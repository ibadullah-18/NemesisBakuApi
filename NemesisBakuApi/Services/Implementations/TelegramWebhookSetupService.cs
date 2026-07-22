using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class TelegramWebhookSetupService : BackgroundService
{
    private readonly ITelegramBotService _telegramBotService;
    private readonly ILogger<TelegramWebhookSetupService> _logger;

    public TelegramWebhookSetupService(
        ITelegramBotService telegramBotService,
        ILogger<TelegramWebhookSetupService> logger)
    {
        _telegramBotService = telegramBotService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_telegramBotService.IsConfigured)
        {
            _logger.LogWarning(
                "Telegram inteqrasiyası aktiv deyil: BotToken və ya WebhookSecret yoxdur.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            await _telegramBotService.ConfigureWebhookAsync(stoppingToken);

            _logger.LogInformation(
                "Telegram webhook uğurla konfiqurasiya edildi.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram webhook konfiqurasiyası uğursuz oldu.");
        }
    }
}
