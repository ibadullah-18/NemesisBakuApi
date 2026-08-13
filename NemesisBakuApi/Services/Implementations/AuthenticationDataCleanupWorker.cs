using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Data;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public sealed class AuthenticationDataCleanupWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly AuthenticationCleanupSettings
        _settings;

    private readonly ILogger<
        AuthenticationDataCleanupWorker> _logger;

    public AuthenticationDataCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AuthenticationCleanupSettings> options,
        ILogger<AuthenticationDataCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var initialDelay = TimeSpan.FromSeconds(
            Math.Max(
                1,
                _settings.InitialDelaySeconds));

        var interval = TimeSpan.FromHours(
            Math.Max(
                1,
                _settings.IntervalHours));

        try
        {
            await Task.Delay(
                initialDelay,
                stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupSafelyAsync(stoppingToken);

                await Task.Delay(
                    interval,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CleanupSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var context = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;

            var otpCutoff = now.AddDays(
                -Math.Max(
                    1,
                    _settings.OtpRetentionDays));

            var refreshTokenCutoff = now.AddDays(
                -Math.Max(
                    1,
                    _settings.RefreshTokenRetentionDays));

            var deletedOtpCount =
                await context.UserOtpCodes
                    .Where(x =>
                        x.ExpiresAt < otpCutoff)
                    .ExecuteDeleteAsync(
                        cancellationToken);

            var deletedRefreshTokenCount =
                await context.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.ExpiresAt <
                            refreshTokenCutoff ||

                        (x.IsUsed &&
                         x.UsedAt.HasValue &&
                         x.UsedAt.Value <
                            refreshTokenCutoff) ||

                        (x.IsRevoked &&
                         x.RevokedAt.HasValue &&
                         x.RevokedAt.Value <
                            refreshTokenCutoff))
                    .ExecuteDeleteAsync(
                        cancellationToken);

            if (deletedOtpCount > 0 ||
                deletedRefreshTokenCount > 0)
            {
                _logger.LogInformation(
                    "Authentication cleanup tamamlandı. " +
                    "Silinən OTP: {OtpCount}, " +
                    "silinən refresh token: " +
                    "{RefreshTokenCount}",
                    deletedOtpCount,
                    deletedRefreshTokenCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Authentication məlumatları " +
                "təmizlənərkən xəta baş verdi");
        }
    }
}