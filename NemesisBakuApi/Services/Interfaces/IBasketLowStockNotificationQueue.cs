namespace NemesisBakuApi.Services.Interfaces;

public sealed record BasketLowStockNotification(
    Guid UserId,
    Guid ProductVariantId);

public interface IBasketLowStockNotificationQueue
{
    bool TryEnqueue(
        BasketLowStockNotification notification);

    IAsyncEnumerable<BasketLowStockNotification>
        ReadAllAsync(
            CancellationToken cancellationToken);
}