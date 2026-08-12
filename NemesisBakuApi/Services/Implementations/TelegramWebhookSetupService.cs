using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class TelegramWebhookSetupService
    : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly ITelegramBotService
        _telegramBotService;

    private readonly ILogger<
        TelegramWebhookSetupService> _logger;

    public TelegramWebhookSetupService(
        ITelegramBotService telegramBotService,
        ILogger<TelegramWebhookSetupService> logger)
    {
        _telegramBotService =
            telegramBotService;

        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_telegramBotService.IsConfigured)
        {
            _logger.LogWarning(
                "Telegram inteqrasiyası aktiv deyil: " +
                "Telegram ayarları tam deyil.");

            return;
        }

        await DelaySafelyAsync(
            TimeSpan.FromSeconds(3),
            stoppingToken);

        for (var attempt = 1;
             attempt <= MaxAttempts;
             attempt++)
        {
            try
            {
                await _telegramBotService
                    .ConfigureWebhookAsync(
                        stoppingToken);

                _logger.LogInformation(
                    "Telegram webhook uğurla " +
                    "konfiqurasiya edildi.");

                return;
            }
            catch (OperationCanceledException)
                when (stoppingToken
                    .IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Telegram webhook konfiqurasiyası " +
                    "uğursuz oldu. Cəhd: " +
                    "{Attempt}/{MaxAttempts}",
                    attempt,
                    MaxAttempts);

                if (attempt == MaxAttempts)
                {
                    break;
                }

                var retryDelay =
                    TimeSpan.FromSeconds(
                        attempt * 5);

                await DelaySafelyAsync(
                    retryDelay,
                    stoppingToken);
            }
        }

        _logger.LogError(
            "Telegram webhook {MaxAttempts} cəhddən " +
            "sonra konfiqurasiya edilə bilmədi. " +
            "API işləməyə davam edir.",
            MaxAttempts);
    }

    private static async Task DelaySafelyAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                delay,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            // Tətbiq bağlanır.
        }
    }
}