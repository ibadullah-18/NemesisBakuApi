using System.Security.Cryptography;
using System.Text;

namespace NemesisBakuApi.Helpers;

public static class RefreshTokenGenerator
{
    private const int TokenSizeInBytes = 64;

    public static string Generate()
    {
        Span<byte> randomBytes =
            stackalloc byte[TokenSizeInBytes];

        RandomNumberGenerator.Fill(randomBytes);

        return Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Hash(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException(
                "Refresh token boş ola bilməz.",
                nameof(token));
        }

        var tokenBytes =
            Encoding.UTF8.GetBytes(token);

        var hashBytes =
            SHA256.HashData(tokenBytes);

        return Convert.ToHexString(hashBytes);
    }
}