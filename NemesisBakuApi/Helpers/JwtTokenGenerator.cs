using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NemesisBakuApi.Entities;

namespace NemesisBakuApi.Helpers;

public class JwtTokenGenerator
{
    private static readonly JwtSecurityTokenHandler
        TokenHandler = new();

    private readonly UserManager<AppUser> _userManager;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly double _expireMinutes;
    private readonly SigningCredentials _credentials;

    public JwtTokenGenerator(
        IConfiguration configuration,
        UserManager<AppUser> userManager)
    {
        _userManager = userManager;

        var jwtSection =
            configuration.GetSection("Jwt");

        var key = jwtSection["Key"];
        _issuer = jwtSection["Issuer"] ?? "";
        _audience = jwtSection["Audience"] ?? "";

        ValidateSettings(
            key,
            _issuer,
            _audience);

        _expireMinutes = ParseExpireMinutes(
            jwtSection["ExpireMinutes"]);

        var securityKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key!));

        _credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);
    }

    public async Task<string> GenerateTokenAsync(
        AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var roles = await _userManager
            .GetRolesAsync(user);

        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Name,
                user.FullName ?? string.Empty),

            new(
                ClaimTypes.MobilePhone,
                user.PhoneNumber ?? string.Empty),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString("N")),

            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now)
                    .ToString(
                        CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        claims.AddRange(
            roles.Select(
                role => new Claim(
                    ClaimTypes.Role,
                    role)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(
                _expireMinutes),
            signingCredentials: _credentials);

        return TokenHandler.WriteToken(token);
    }

    private static void ValidateSettings(
        string? key,
        string issuer,
        string audience)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "JWT Key konfiqurasiya edilməyib.");
        }

        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "JWT Key minimum 32 bayt olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException(
                "JWT Issuer konfiqurasiya edilməyib.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "JWT Audience konfiqurasiya edilməyib.");
        }
    }

    private static double ParseExpireMinutes(
        string? value)
    {
        var parsed = double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var minutes);

        if (!parsed || minutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT ExpireMinutes düzgün deyil.");
        }

        return minutes;
    }
}