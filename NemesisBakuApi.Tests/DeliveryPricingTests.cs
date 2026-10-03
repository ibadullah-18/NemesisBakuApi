using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NemesisBakuApi.Controllers;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Implementations;
using NemesisBakuApi.Settings;
using Xunit;

namespace NemesisBakuApi.Tests;

public class DeliveryPricingTests
{
    private static readonly DeliverySettings Settings = new() { MinimumPrice = 5, PricePerKm = 2 };
    private static MetroStation Station(decimal latitude = 0, bool active = true) => new()
    { Name = "Test", Latitude = latitude, Longitude = 0, IsActive = active };

    [Theory]
    [InlineData(0, 6)]
    [InlineData(0.99999, 6)]
    [InlineData(1, 6)]
    [InlineData(1.00001, 7)]
    [InlineData(1.99999, 7)]
    [InlineData(2, 7)]
    public void Tariff_uses_unrounded_distance(double km, int price)
    {
        var latitude = (decimal)(km / 6371d * 180d / Math.PI);
        var quote = DeliveryPricingService.QuoteHome(latitude, 0, [(Station(), (decimal)km)], 12m, Settings);
        Assert.Equal(price, quote.DeliveryPrice);
    }

    [Fact]
    public void Just_beyond_two_km_uses_existing_store_tariff()
    {
        var latitude = 0m;
        var quote = DeliveryPricingService.QuoteHome(latitude, 0, [(Station(), 2.00001m)], 12m, Settings);
        Assert.Equal("store-road", quote.PricingRule);
        Assert.Equal(DeliveryPriceCalculator.CalculateDeliveryPrice(quote.DistanceKm!.Value, Settings), quote.DeliveryPrice);
    }

    [Fact]
    public void Nearest_active_station_is_selected_and_deleted_stations_are_ignored()
    {
        var far = Station(.02m);
        var near = Station(.008m);
        var deleted = Station(); deleted.IsDeleted = true;
        var quote = DeliveryPricingService.QuoteHome(0, 0, [(far, 1.4m), (Station(0, false), 0m), (deleted, 0m), (near, .9m)], 10m, Settings);
        Assert.Equal(near.Id, quote.MetroStationId);
        Assert.Equal(6, quote.DeliveryPrice);
    }

    [Fact]
    public void No_active_metro_uses_legacy_minimum_price()
    {
        var quote = DeliveryPricingService.QuoteHome(0, 0, [(Station(0, false), 0m)], 0m, Settings);
        Assert.Equal("store-road", quote.PricingRule);
        Assert.Equal(5, quote.DeliveryPrice);
        Assert.Null(quote.MetroStationId);
    }

    [Fact]
    public void Missing_store_coordinates_only_block_legacy_pricing()
    {
        Assert.Equal(6, DeliveryPricingService.QuoteHome(0, 0, [(Station(), .5m)], null, Settings).DeliveryPrice);
        Assert.Throws<RoadRoutingException>(() => DeliveryPricingService.QuoteHome(1, 1, [], null, Settings));
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    public void Invalid_coordinates_rejected(decimal latitude, decimal longitude) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DeliveryPricingService.QuoteHome(latitude, longitude, [], 0m, Settings));

    [Fact]
    public async Task Seeded_stations_and_server_authoritative_metro_pickup()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var stations = await db.MetroStations.ToListAsync();
        Assert.Equal(27, stations.Count);
        Assert.Equal(27, stations.Select(s => s.Name).Distinct().Count());
        Assert.All(stations, s => { Assert.InRange(s.Latitude, 40m, 41m); Assert.InRange(s.Longitude, 49m, 51m); });
        var service = new DeliveryPricingService(db, Options.Create(Settings), new FakeRoads());
        foreach (var station in stations)
        {
            var quote = await service.QuoteAsync(new() { DeliveryType = DeliveryType.MetroPickup, MetroStationId = station.Id, Latitude = 0, Longitude = 0 });
            Assert.Equal(4, quote.DeliveryPrice);
            Assert.Equal(station.Latitude, quote.Latitude);
            Assert.Equal(station.Longitude, quote.Longitude);
            Assert.Equal(station.Name, quote.MetroStationName);
        }
        var selected = stations[0];
        selected.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.QuoteAsync(new() { DeliveryType = DeliveryType.MetroPickup, MetroStationId = selected.Id }));
        Assert.Equal(0, (await service.QuoteAsync(new() { DeliveryType = DeliveryType.PickupFromStore })).DeliveryPrice);
        var active = stations[1];
        var home = await service.QuoteAsync(new() { DeliveryType = DeliveryType.HomeDelivery, Latitude = active.Latitude, Longitude = active.Longitude, MetroStationId = selected.Id });
        Assert.NotEqual(selected.Id, home.MetroStationId);
        Assert.Equal(6, home.DeliveryPrice);
    }

    [Fact]
    public async Task Admin_can_create_edit_deactivate_but_stale_updates_are_rejected()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var admin = new AdminMetroStationsController(db, new FailingAudit(), NullLogger<AdminMetroStationsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        // An unavailable audit sink must not report a successful save as a failure.
        Assert.IsType<OkObjectResult>(await admin.Create(new() { Name = "Yeni metro", Latitude = 40.4m, Longitude = 49.8m }, default));
        var station = await db.MetroStations.SingleAsync(s => s.Name == "Yeni metro");
        var originalVersion = station.Version;
        Assert.IsType<OkObjectResult>(await admin.Update(station.Id, new() { Name = station.Name, Latitude = 40.5m, Longitude = 49.9m, IsActive = false, Version = originalVersion }, default));
        Assert.False(station.IsActive);
        Assert.Equal(40.5m, station.Latitude);
        Assert.IsType<ConflictObjectResult>(await admin.Update(station.Id, new() { Name = station.Name, Latitude = 40.4m, Longitude = 49.8m, Version = originalVersion }, default));
        Assert.IsType<ConflictObjectResult>(await admin.Create(new() { Name = station.Name, Latitude = 40.4m, Longitude = 49.8m }, default));
        Assert.IsType<BadRequestObjectResult>(await admin.Create(new() { Name = "Invalid", Latitude = 91, Longitude = 0 }, default));
    }

    private sealed class FakeRoads : IRoadDistanceService
    {
        public Task<IReadOnlyList<decimal?>> GetDistancesKmAsync(IReadOnlyList<RoadPoint> origins, RoadPoint destination, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<decimal?>>(origins.Select(_ => (decimal?).75m).ToArray());
    }

    [Fact]
    public void Road_distance_can_choose_a_geographically_more_distant_station()
    {
        var close = Station(0);
        var farther = Station(.03m);
        var quote = DeliveryPricingService.QuoteHome(0, 0, [(close, 2.4m), (farther, .9m)], 8m, Settings);
        Assert.Equal(farther.Id, quote.MetroStationId);
        Assert.Equal(6m, quote.DeliveryPrice);
        Assert.Equal("metro-0-1km-road", quote.PricingRule);
    }

    [Fact]
    public async Task Missing_osrm_configuration_returns_503_even_at_metro_coordinates()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var metro = await db.MetroStations.FirstAsync();
        using var http = new HttpClient();
        var pricing = new DeliveryPricingService(db, Options.Create(Settings), new OsrmRoadDistanceService(http, Options.Create(new RoadRoutingSettings())));
        var controller = new OrdersController(db, null!, pricing) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var result = Assert.IsType<ObjectResult>(await controller.CalculateDelivery(new() { Latitude = metro.Latitude, Longitude = metro.Longitude }));
        Assert.Equal(503, result.StatusCode);
    }
    private sealed class FailingAudit : IAuditLogService
    {
        public Task CreateAsync(Guid? userId, string action, string entityName, string? entityId, string? description, string? ipAddress, string? userAgent) =>
            throw new InvalidOperationException("Test audit outage");
    }
}
