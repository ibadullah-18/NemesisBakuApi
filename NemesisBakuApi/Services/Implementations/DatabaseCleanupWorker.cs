using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public sealed class DatabaseCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseCleanupSettings _settings;
    private readonly ILogger<DatabaseCleanupWorker> _logger;

    public DatabaseCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DatabaseCleanupSettings> options,
        ILogger<DatabaseCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation(
                "Database cleanup worker söndürülüb.");

            return;
        }

        var initialDelay = TimeSpan.FromSeconds(
            Math.Clamp(
                _settings.InitialDelaySeconds,
                1,
                3600));

        var interval = TimeSpan.FromHours(
            Math.Clamp(
                _settings.IntervalHours,
                1,
                168));

        try
        {
            await Task.Delay(initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupSafelyAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
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

            var cleanupService = scope.ServiceProvider
                .GetRequiredService<IDatabaseCleanupService>();

            var result = await cleanupService.CleanupAsync(
                cancellationToken);

            if (result.TotalDeleted == 0)
            {
                _logger.LogDebug(
                    "Database cleanup tamamlandı, silinəcək köhnə məlumat yoxdur.");

                return;
            }

            var details = string.Join(
                ", ",
                result.DeletedByTable
                    .Where(x => x.Value > 0)
                    .Select(x => $"{x.Key}: {x.Value}"));

            _logger.LogInformation(
                "Database cleanup tamamlandı. Cəmi: {TotalDeleted}. {Details}",
                result.TotalDeleted,
                details);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Database cleanup zamanı xəta baş verdi.");
        }
    }
}