using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class DeliveryPricingService(AppDbContext db, IOptions<DeliverySettings> options)
{
    public async Task<CalculateDeliveryResultDto> QuoteAsync(CalculateDeliveryDto input, CancellationToken ct = default)
    {
        if (input.DeliveryType == DeliveryType.PickupFromStore)
            return new() { DeliveryPrice = 0, PricingRule = "store-pickup" };
        if (input.DeliveryType == DeliveryType.MetroPickup)
        {
            var station = await db.MetroStations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.MetroStationId && x.IsActive, ct);
            if (station == null) throw new ArgumentException("Təhvil üçün aktiv metro stansiyası seçin.");
            return new() { DeliveryPrice = 4, PricingRule = "metro-pickup", MetroStationId = station.Id,
                MetroStationName = station.Name, MetroDistanceKm = 0, Latitude = station.Latitude, Longitude = station.Longitude };
        }
        if (input.DeliveryType != DeliveryType.HomeDelivery) throw new ArgumentException("Çatdırılma növü düzgün deyil.");
        if (!input.Latitude.HasValue || !input.Longitude.HasValue) throw new ArgumentException("Xəritədən ünvan seçin.");
        var stations = await db.MetroStations.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var store = await db.StoreInfos.AsNoTracking().FirstOrDefaultAsync(ct);
        return QuoteHome(input.Latitude.Value, input.Longitude.Value, stations, store?.Latitude, store?.Longitude, options.Value);
    }

    public static CalculateDeliveryResultDto QuoteHome(decimal latitude, decimal longitude,
        IEnumerable<MetroStation> stations, decimal? storeLatitude, decimal? storeLongitude, DeliverySettings settings)
    {
        DeliveryPriceCalculator.CalculateDistanceKm(latitude, longitude, latitude, longitude, false);
        var nearest = stations.Where(s => s.IsActive && !s.IsDeleted)
            .Select(s => new { Station = s, Distance = DeliveryPriceCalculator.CalculateDistanceKm(s.Latitude, s.Longitude, latitude, longitude, false) })
            .OrderBy(s => s.Distance).ThenBy(s => s.Station.Id).FirstOrDefault();
        decimal? storeDistance = storeLatitude.HasValue && storeLongitude.HasValue
            ? DeliveryPriceCalculator.CalculateDistanceKm(storeLatitude.Value, storeLongitude.Value, latitude, longitude) : null;
        var result = new CalculateDeliveryResultDto
        {
            Latitude = latitude, Longitude = longitude, DistanceKm = storeDistance,
            MetroStationId = nearest?.Station.Id, MetroStationName = nearest?.Station.Name,
            MetroDistanceKm = nearest == null ? null : Math.Round(nearest.Distance, 4, MidpointRounding.AwayFromZero)
        };
        // Compare full precision: 1.004 km must not round into the cheaper band.
        if (nearest?.Distance <= 1m) { result.DeliveryPrice = 6; result.PricingRule = "metro-0-1km"; }
        else if (nearest?.Distance <= 2m) { result.DeliveryPrice = 7; result.PricingRule = "metro-1-2km"; }
        else
        {
            if (!storeDistance.HasValue) throw new ArgumentException("Mağaza koordinatları təyin edilməyib.");
            result.DeliveryPrice = DeliveryPriceCalculator.CalculateDeliveryPrice(storeDistance.Value, settings);
            result.PricingRule = "store-distance";
        }
        return result;
    }
}
