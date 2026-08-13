using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Data.Interceptors;

public sealed class ProductCacheInvalidationInterceptor
    : SaveChangesInterceptor
{
    private readonly IOutputCacheStore _outputCacheStore;

    private readonly ILogger<
        ProductCacheInvalidationInterceptor> _logger;

    private bool _invalidateAfterSave;

    public ProductCacheInvalidationInterceptor(
        IOutputCacheStore outputCacheStore,
        ILogger<ProductCacheInvalidationInterceptor> logger)
    {
        _outputCacheStore = outputCacheStore;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureChanges(eventData.Context);

        return base.SavingChanges(
            eventData,
            result);
    }

    public override ValueTask<InterceptionResult<int>>
        SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
    {
        CaptureChanges(eventData.Context);

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        EvictSafelyAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return base.SavedChanges(
            eventData,
            result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await EvictSafelyAsync(cancellationToken);

        return await base.SavedChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    public override void SaveChangesFailed(
        DbContextErrorEventData eventData)
    {
        _invalidateAfterSave = false;

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _invalidateAfterSave = false;

        return base.SaveChangesFailedAsync(
            eventData,
            cancellationToken);
    }

    private void CaptureChanges(
        DbContext? context)
    {
        if (_invalidateAfterSave ||
            context == null)
        {
            return;
        }

        _invalidateAfterSave =
            context.ChangeTracker
                .Entries()
                .Any(entry =>
                    (entry.State == EntityState.Added ||
                     entry.State == EntityState.Modified ||
                     entry.State == EntityState.Deleted) &&
                    IsProductRelated(entry.Entity));
    }

    private static bool IsProductRelated(
        object entity)
    {
        return entity is
            Product or
            ProductImage or
            ProductVariant or
            Category or
            Brand or
            Size or
            Color;
    }

    private async Task EvictSafelyAsync(
        CancellationToken cancellationToken)
    {
        if (!_invalidateAfterSave)
        {
            return;
        }

        _invalidateAfterSave = false;

        try
        {
            await _outputCacheStore.EvictByTagAsync(
                ProductCacheTags.Tag,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Məhsul output cache-i təmizlənə bilmədi");
        }
    }
}