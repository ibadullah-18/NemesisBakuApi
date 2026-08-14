using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public sealed class ProductViewTracker
    : BackgroundService,
      IProductViewTracker
{
    private const int MaximumTrackedViewers = 50_000;
    private const int MaximumPendingProducts = 10_000;

    private static readonly TimeSpan DeduplicationWindow =
        TimeSpan.FromMinutes(30);

    private static readonly TimeSpan FlushInterval =
        TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<Guid, int>
        _pendingViews = new();

    private readonly ConcurrentDictionary<ViewDeduplicationKey, long>
        _recentViews = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductViewTracker> _logger;

    public ProductViewTracker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductViewTracker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool TryTrackView(
        Guid productId,
        string viewerKey)
    {
        if (productId == Guid.Empty ||
            string.IsNullOrWhiteSpace(viewerKey))
        {
            return false;
        }

        if (_pendingViews.Count >= MaximumPendingProducts &&
            !_pendingViews.ContainsKey(productId))
        {
            return false;
        }

        var key = new ViewDeduplicationKey(
            productId,
            HashViewerKey(viewerKey));

        var nowTicks = DateTime.UtcNow.Ticks;
        var cutoffTicks =
            nowTicks - DeduplicationWindow.Ticks;

        while (true)
        {
            if (_recentViews.TryGetValue(
                    key,
                    out var previousTicks))
            {
                if (previousTicks >= cutoffTicks)
                {
                    return false;
                }

                if (_recentViews.TryUpdate(
                        key,
                        nowTicks,
                        previousTicks))
                {
                    break;
                }

                continue;
            }

            if (_recentViews.Count >= MaximumTrackedViewers)
            {
                return false;
            }

            if (_recentViews.TryAdd(key, nowTicks))
            {
                break;
            }
        }

        _pendingViews.AddOrUpdate(
            productId,
            1,
            static (_, current) =>
                current == int.MaxValue
                    ? current
                    : current + 1);

        return true;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            FlushInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                RemoveExpiredViewerKeys(
                    DateTime.UtcNow.Ticks -
                    DeduplicationWindow.Ticks);

                await FlushSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushSafelyAsync(CancellationToken.None);
    }

    private async Task FlushSafelyAsync(
        CancellationToken cancellationToken)
    {
        var batch = DrainPendingViews();

        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var context = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var productIds = batch.Keys.ToList();

            var products = await context.Products
                .Where(x =>
                    x.IsActive &&
                    productIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var product in products)
            {
                var increment = batch[product.Id];

                product.ViewCount = (int)Math.Min(
                    (long)product.ViewCount + increment,
                    int.MaxValue);
            }

            await context.SaveChangesAsync(
                cancellationToken);

            _logger.LogDebug(
                "Məhsul baxışları batch yazıldı. ProductCount: {ProductCount}, ViewCount: {ViewCount}",
                products.Count,
                batch.Values.Sum(x => (long)x));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            RestorePendingViews(batch);
        }
        catch (Exception exception)
        {
            RestorePendingViews(batch);

            _logger.LogError(
                exception,
                "Məhsul baxışları database-ə yazılmadı, batch yenidən növbəyə qaytarıldı.");
        }
    }

    private Dictionary<Guid, int> DrainPendingViews()
    {
        var result = new Dictionary<Guid, int>();

        foreach (var productId in _pendingViews.Keys)
        {
            if (_pendingViews.TryRemove(
                    productId,
                    out var count))
            {
                result[productId] = count;
            }
        }

        return result;
    }

    private void RestorePendingViews(
        IReadOnlyDictionary<Guid, int> batch)
    {
        foreach (var item in batch)
        {
            _pendingViews.AddOrUpdate(
                item.Key,
                item.Value,
                (_, current) => (int)Math.Min(
                    (long)current + item.Value,
                    int.MaxValue));
        }
    }

    private void RemoveExpiredViewerKeys(long cutoffTicks)
    {
        foreach (var item in _recentViews)
        {
            if (item.Value >= cutoffTicks)
            {
                continue;
            }

            ((ICollection<KeyValuePair<
                    ViewDeduplicationKey,
                    long>>)_recentViews)
                .Remove(item);
        }
    }

    private static string HashViewerKey(string viewerKey)
    {
        var bytes = Encoding.UTF8.GetBytes(viewerKey);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private readonly record struct ViewDeduplicationKey(
        Guid ProductId,
        string ViewerHash);
}