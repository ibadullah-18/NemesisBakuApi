using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public sealed class BasketLowStockNotificationWorker
    : BackgroundService
{
    private readonly IBasketLowStockNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<
        BasketLowStockNotificationWorker> _logger;

    public BasketLowStockNotificationWorker(
        IBasketLowStockNotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BasketLowStockNotificationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var notification in
                _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessSafelyAsync(
                    notification,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessSafelyAsync(
        BasketLowStockNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessAsync(
                notification,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Səbət aşağı stok bildirişi göndərilmədi. " +
                "UserId: {UserId}, VariantId: {VariantId}",
                notification.UserId,
                notification.ProductVariantId);
        }
    }

    private async Task ProcessAsync(
        BasketLowStockNotification notification,
        CancellationToken cancellationToken)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var alreadySent = await context
            .BasketLowStockEmailLogs
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.UserId == notification.UserId &&
                    x.ProductVariantId ==
                    notification.ProductVariantId,
                cancellationToken);

        if (alreadySent)
        {
            return;
        }

        var item = await context.BasketItems
            .AsNoTracking()
            .Where(x =>
                x.UserId == notification.UserId &&
                x.ProductVariantId ==
                notification.ProductVariantId &&
                x.Product.IsActive &&
                x.ProductVariant.IsActive &&
                x.ProductVariant.StockCount > 0 &&
                x.ProductVariant.StockCount <= 3)
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product.Name,
                StockCount = x.ProductVariant.StockCount,
                Email = x.User.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item == null ||
            string.IsNullOrWhiteSpace(item.Email))
        {
            return;
        }

        var emailService = scope.ServiceProvider
            .GetRequiredService<IEmailService>();

        var productLink =
            "https://nemesisbaku.az/products/" +
            item.ProductId;

        var sent = await emailService
            .SendBasketLowStockAsync(
                item.Email,
                item.ProductName,
                productLink,
                item.StockCount);

        if (!sent)
        {
            _logger.LogWarning(
                "Səbət aşağı stok email-i göndərilmədi. " +
                "UserId: {UserId}, VariantId: {VariantId}",
                notification.UserId,
                notification.ProductVariantId);

            return;
        }

        context.BasketLowStockEmailLogs.Add(
            new BasketLowStockEmailLog
            {
                UserId = notification.UserId,
                ProductId = item.ProductId,

                ProductVariantId =
                    notification.ProductVariantId,

                Email = item.Email,
                StockCountAtSend = item.StockCount,
                SentAt = DateTime.UtcNow
            });

        try
        {
            await context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            // Eyni bildiriş artıq başqa sorğudan yazılıb.
            context.ChangeTracker.Clear();
        }
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException
        {
            Number: 2601 or 2627
        };
    }
}