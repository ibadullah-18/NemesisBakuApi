using System.Security.Cryptography;

namespace NemesisBakuApi.Helpers;

public static class RefreshTokenGenerator
{
    public static string Generate()
    {
        var randomBytes = new byte[64];

        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        return Convert.ToBase64String(randomBytes);
    }
}