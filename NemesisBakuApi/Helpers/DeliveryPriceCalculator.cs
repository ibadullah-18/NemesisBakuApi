using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Helpers;

public static class DeliveryPriceCalculator
{
    private const double EarthRadiusKm = 6371d;

    public static decimal CalculateDistanceKm(
        decimal storeLat,
        decimal storeLng,
        decimal customerLat,
        decimal customerLng)
    {
        ValidateCoordinates(
            storeLat,
            storeLng,
            nameof(storeLat),
            nameof(storeLng));

        ValidateCoordinates(
            customerLat,
            customerLng,
            nameof(customerLat),
            nameof(customerLng));

        var storeLatitude =
            DegreesToRadians((double)storeLat);

        var storeLongitude =
            DegreesToRadians((double)storeLng);

        var customerLatitude =
            DegreesToRadians((double)customerLat);

        var customerLongitude =
            DegreesToRadians((double)customerLng);

        var latitudeDifference =
            customerLatitude - storeLatitude;

        var longitudeDifference =
            customerLongitude - storeLongitude;

        var latitudeSin =
            Math.Sin(latitudeDifference / 2d);

        var longitudeSin =
            Math.Sin(longitudeDifference / 2d);

        var haversine =
            latitudeSin * latitudeSin +
            Math.Cos(storeLatitude) *
            Math.Cos(customerLatitude) *
            longitudeSin * longitudeSin;

        haversine = Math.Clamp(
            haversine,
            0d,
            1d);

        var centralAngle = 2d * Math.Atan2(
            Math.Sqrt(haversine),
            Math.Sqrt(1d - haversine));

        var distance =
            EarthRadiusKm * centralAngle;

        return Math.Round(
            (decimal)distance,
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateDeliveryPrice(
        decimal distanceKm,
        DeliverySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (distanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceKm),
                "Məsafə mənfi ola bilməz.");
        }

        if (settings.MinimumPrice < 0)
        {
            throw new InvalidOperationException(
                "Minimum çatdırılma qiyməti " +
                "mənfi ola bilməz.");
        }

        if (settings.PricePerKm < 0)
        {
            throw new InvalidOperationException(
                "Kilometr qiyməti mənfi ola bilməz.");
        }

        var calculatedPrice =
            distanceKm * settings.PricePerKm;

        var finalPrice = Math.Max(
            calculatedPrice,
            settings.MinimumPrice);

        return Math.Round(
            finalPrice,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static void ValidateCoordinates(
        decimal latitude,
        decimal longitude,
        string latitudeParameter,
        string longitudeParameter)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                latitudeParameter,
                "Enlik -90 və 90 arasında olmalıdır.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                longitudeParameter,
                "Uzunluq -180 və 180 arasında olmalıdır.");
        }
    }

    private static double DegreesToRadians(
        double degrees)
    {
        return degrees * Math.PI / 180d;
    }
}