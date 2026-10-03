using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Helpers;

public static class DeliveryPriceCalculator
{
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

}
