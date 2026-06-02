using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Helpers;

public static class DeliveryPriceCalculator
{
    public static decimal CalculateDistanceKm(
        decimal storeLat,
        decimal storeLng,
        decimal customerLat,
        decimal customerLng)
    {
        const double earthRadiusKm = 6371;

        var lat1 = DegreesToRadians((double)storeLat);
        var lon1 = DegreesToRadians((double)storeLng);
        var lat2 = DegreesToRadians((double)customerLat);
        var lon2 = DegreesToRadians((double)customerLng);

        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round((decimal)(earthRadiusKm * c), 2);
    }

    public static decimal CalculateDeliveryPrice(decimal distanceKm, DeliverySettings settings)
    {
        var price = distanceKm * settings.PricePerKm;

        if (price < settings.MinimumPrice)
            price = settings.MinimumPrice;

        return Math.Round(price, 2);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}