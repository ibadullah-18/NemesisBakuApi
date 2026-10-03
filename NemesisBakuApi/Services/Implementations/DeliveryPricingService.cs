using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Settings;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class DeliveryPricingService(AppDbContext db, IOptions<DeliverySettings> options, IRoadDistanceService roads)
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
        if (input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180) throw new ArgumentException("Koordinatlar düzgün deyil.");
        var stations = await db.MetroStations.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var store = await db.StoreInfos.AsNoTracking().FirstOrDefaultAsync(ct);
        var origins = stations.Select(s => new RoadPoint(s.Latitude, s.Longitude)).ToList();
        var hasStore = store?.Latitude != null && store.Longitude != null;
        if (hasStore) origins.Add(new(store!.Latitude!.Value, store.Longitude!.Value));
        if (origins.Count == 0) throw new RoadRoutingException("Çatdırılma marşrutu üçün nöqtələr təyin edilməyib.");
        var distances = await roads.GetDistancesKmAsync(origins, new(input.Latitude.Value, input.Longitude.Value), ct);
        if (distances.Count != origins.Count || distances.Any(d => d < 0)) throw new RoadRoutingException("Yol məsafəsi düzgün alınmadı.");
        return QuoteHome(input.Latitude.Value, input.Longitude.Value,
            stations.Select((s, i) => (s, distances[i])), hasStore ? distances[^1] : null, options.Value);
    }

    public static CalculateDeliveryResultDto QuoteHome(decimal latitude, decimal longitude,
        IEnumerable<(MetroStation Station, decimal? DistanceKm)> routes, decimal? storeDistance, DeliverySettings settings)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(latitude));
        var nearest = routes.Where(s => s.Station.IsActive && !s.Station.IsDeleted && s.DistanceKm.HasValue && s.DistanceKm >= 0)
            .Select(s => new { s.Station, Distance = s.DistanceKm!.Value })
            .OrderBy(s => s.Distance).ThenBy(s => s.Station.Id).FirstOrDefault();
        var result = new CalculateDeliveryResultDto
        {
            Latitude = latitude, Longitude = longitude, DistanceKm = storeDistance.HasValue ? Math.Round(storeDistance.Value, 4) : null,
            MetroStationId = nearest?.Station.Id, MetroStationName = nearest?.Station.Name,
            MetroDistanceKm = nearest == null ? null : Math.Round(nearest.Distance, 4, MidpointRounding.AwayFromZero)
        };
        // Compare full precision: 1.004 km must not round into the cheaper band.
        if (nearest?.Distance <= 1m) { result.DeliveryPrice = 6; result.PricingRule = "metro-0-1km-road"; }
        else if (nearest?.Distance <= 2m) { result.DeliveryPrice = 7; result.PricingRule = "metro-1-2km-road"; }
        else
        {
            if (!storeDistance.HasValue || storeDistance < 0) throw new RoadRoutingException("Mağazadan ünvana yol marşrutu tapılmadı. Xəritədə nöqtəni yoxlayın.");
            result.DeliveryPrice = DeliveryPriceCalculator.CalculateDeliveryPrice(storeDistance.Value, settings);
            result.PricingRule = "store-road";
        }
        return result;
    }
}
