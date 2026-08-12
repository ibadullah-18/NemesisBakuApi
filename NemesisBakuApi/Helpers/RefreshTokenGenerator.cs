using System.Security.Cryptography;

namespace NemesisBakuApi.Helpers;

public static class RefreshTokenGenerator
{
    private const int TokenSizeInBytes = 64;

    public static string Generate()
    {
        Span<byte> randomBytes =
            stackalloc byte[TokenSizeInBytes];

        RandomNumberGenerator.Fill(
            randomBytes);

        return Convert.ToBase64String(
            randomBytes);
    }
}