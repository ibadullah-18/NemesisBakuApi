using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public class TelegramOrderNotificationWorker
    : BackgroundService
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 8;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<
        TelegramOrderNotificationWorker> _logger;

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
        using var timer = new PeriodicTimer(
            PollInterval);

        await ProcessSafelyAsync(stoppingToken);

        try
        {
            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                await ProcessSafelyAsync(
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Tətbiq normal şəkildə bağlanır.
        }
    }

    private async Task ProcessSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessBatchAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram sifariş bildiriş worker-i " +
                "zamanı xəta baş verdi.");
        }
    }

    private async Task ProcessBatchAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var telegram = scope.ServiceProvider
            .GetRequiredService<ITelegramBotService>();

        if (!telegram.IsConfigured)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var notifications =
            await context.TelegramOrderNotifications
                .Where(x =>
                    x.SentAt == null &&
                    x.AttemptCount < MaxAttempts &&
                    (!x.NextAttemptAt.HasValue ||
                     x.NextAttemptAt <= now))
                .OrderBy(x => x.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return;
        }

        var orderIds = notifications
            .Select(x => x.OrderId)
            .Distinct()
            .ToList();

        var orders = await context.Orders
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.CustomerFullName,
                x.TotalPrice,

                ProductCount = x.Items.Sum(
                    item => item.Quantity)
            })
            .ToDictionaryAsync(
                x => x.Id,
                cancellationToken);

        foreach (var notification in notifications)
        {
            if (cancellationToken
                .IsCancellationRequested)
            {
                break;
            }

            if (!orders.TryGetValue(
                    notification.OrderId,
                    out var order))
            {
                MarkAsPermanentlyFailed(
                    notification,
                    "Sifariş tapılmadı.");

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
                    order.ProductCount,
                    order.TotalPrice,
                    cancellationToken);

                MarkAsSent(notification);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                MarkAsFailed(
                    notification,
                    ex.Message);

                _logger.LogWarning(
                    ex,
                    "Telegram bildirişi göndərilmədi. " +
                    "NotificationId: {NotificationId}, " +
                    "Attempt: {Attempt}",
                    notification.Id,
                    notification.AttemptCount);
            }
        }

        await context.SaveChangesAsync(
            cancellationToken);
    }

    private static void MarkAsSent(
        Entities.TelegramOrderNotification notification)
    {
        var now = DateTime.UtcNow;

        notification.SentAt = now;
        notification.NextAttemptAt = null;
        notification.LastError = null;
        notification.UpdatedAt = now;
    }

    private static void MarkAsFailed(
        Entities.TelegramOrderNotification notification,
        string error)
    {
        var now = DateTime.UtcNow;

        notification.AttemptCount++;
        notification.LastError =
            Limit(error, 1000);

        notification.UpdatedAt = now;

        notification.NextAttemptAt =
            notification.AttemptCount >= MaxAttempts
                ? null
                : now.Add(
                    GetRetryDelay(
                        notification.AttemptCount));
    }

    private static void MarkAsPermanentlyFailed(
        Entities.TelegramOrderNotification notification,
        string error)
    {
        notification.AttemptCount = MaxAttempts;
        notification.NextAttemptAt = null;
        notification.LastError =
            Limit(error, 1000);

        notification.UpdatedAt = DateTime.UtcNow;
    }

    private static TimeSpan GetRetryDelay(
        int attempt)
    {
        var seconds = Math.Min(
            Math.Pow(2, attempt) * 15,
            3600);

        return TimeSpan.FromSeconds(seconds);
    }

    private static string Limit(
        string value,
        int maxLength)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}