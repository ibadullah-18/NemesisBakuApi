using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class TelegramOrderNotificationWorker : BackgroundService
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 8;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramOrderNotificationWorker> _logger;

    public TelegramOrderNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramOrderNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Telegram sifariş bildiriş worker-i zamanı xəta baş verdi.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegram = scope.ServiceProvider
            .GetRequiredService<ITelegramBotService>();

        if (!telegram.IsConfigured)
            return;

        var now = DateTime.UtcNow;

        var notifications = await context.TelegramOrderNotifications
            .Where(x =>
                x.SentAt == null &&
                x.AttemptCount < MaxAttempts &&
                (!x.NextAttemptAt.HasValue || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
            return;

        var orderIds = notifications
            .Select(x => x.OrderId)
            .Distinct()
            .ToList();

        var orders = await context.Orders
            .Include(x => x.Items)
            .Where(x => orderIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var notification in notifications)
        {
            if (!orders.TryGetValue(notification.OrderId, out var order))
            {
                notification.AttemptCount = MaxAttempts;
                notification.LastError = "Sifariş tapılmadı.";
                notification.UpdatedAt = DateTime.UtcNow;
                continue;
            }

            try
            {
                await telegram.SendNewOrderAsync(
                    notification.TelegramChatId,
                    notification.AdminFullName,
                    notification.PanelRole,
                    order.Id,
                    order.CustomerFullName,
                    order.Items.Sum(x => x.Quantity),
                    order.TotalPrice,
                    cancellationToken);

                notification.SentAt = DateTime.UtcNow;
                notification.LastError = null;
                notification.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                notification.AttemptCount++;
                notification.NextAttemptAt = DateTime.UtcNow.Add(
                    RetryDelay(notification.AttemptCount));
                notification.LastError = Limit(ex.Message, 1000);
                notification.UpdatedAt = DateTime.UtcNow;

                _logger.LogWarning(
                    ex,
                    "Telegram bildirişi göndərilmədi. NotificationId: {NotificationId}, Attempt: {Attempt}",
                    notification.Id,
                    notification.AttemptCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan RetryDelay(int attempt)
    {
        var seconds = Math.Min(Math.Pow(2, attempt) * 15, 3600);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Limit(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
