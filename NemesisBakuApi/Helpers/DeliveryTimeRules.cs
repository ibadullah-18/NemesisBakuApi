using System.Globalization;
namespace NemesisBakuApi.Helpers;
public static class DeliveryTimeRules
{
    public static bool RequiresExactTime(string? pricingRule) => pricingRule?.StartsWith("metro-", StringComparison.Ordinal) == true;
    public static bool IsExactTime(string? value) => value?.Length == 5 && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
