namespace NemesisBakuApi.Helpers;

public static class OrderNumberGenerator
{
    public static string Generate()
    {
        return $"NB-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
    }
}