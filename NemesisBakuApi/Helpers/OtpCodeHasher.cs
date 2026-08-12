using System.Security.Cryptography;
using System.Text;
using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Helpers;

public sealed class OtpCodeHasher
{
    private readonly byte[] _key;

    public OtpCodeHasher(
        IConfiguration configuration)
    {
        var key =
            configuration["Otp:HashKey"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Otp:HashKey konfiqurasiya edilməyib.");
        }

        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "Otp:HashKey minimum 32 bayt olmalıdır.");
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    public string Hash(
        string email,
        OtpPurpose purpose,
        string code)
    {
        var normalizedEmail =
            NormalizeEmail(email);

        var value =
            $"{normalizedEmail}|{(int)purpose}|{code}";

        using var hmac =
            new HMACSHA256(_key);

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash);
    }

    public bool Verify(
        string email,
        OtpPurpose purpose,
        string code,
        string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var expectedHash = Hash(
            email,
            purpose,
            code);

        try
        {
            var expectedBytes =
                Convert.FromHexString(expectedHash);

            var storedBytes =
                Convert.FromHexString(storedHash);

            return CryptographicOperations
                .FixedTimeEquals(
                    expectedBytes,
                    storedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeEmail(
        string email)
    {
        return email
            .Trim()
            .ToLowerInvariant();
    }
}