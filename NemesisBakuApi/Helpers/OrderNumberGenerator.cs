using System.Security.Cryptography;

namespace NemesisBakuApi.Helpers;

public static class OrderNumberGenerator
{
    private const int MinimumRandomNumber = 1000;
    private const int MaximumRandomNumberExclusive = 10000;

    public static string Generate()
    {
        var timestamp = DateTime.UtcNow
            .ToString("yyyyMMddHHmmss");

        var randomNumber =
            RandomNumberGenerator.GetInt32(
                MinimumRandomNumber,
                MaximumRandomNumberExclusive);

        return $"NB-{timestamp}-{randomNumber}";
    }
}