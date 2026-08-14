using System.Threading.Channels;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Services.Implementations;

public sealed class BasketLowStockNotificationQueue
    : IBasketLowStockNotificationQueue
{
    private const int Capacity = 500;

    private readonly Channel<BasketLowStockNotification>
        _channel;

    public BasketLowStockNotificationQueue()
    {
        _channel = Channel.CreateBounded<
            BasketLowStockNotification>(
            new BoundedChannelOptions(Capacity)
            {
                SingleReader = true,
                SingleWriter = false,

                FullMode =
                    BoundedChannelFullMode.DropOldest,

                AllowSynchronousContinuations = false
            });
    }

    public bool TryEnqueue(
        BasketLowStockNotification notification)
    {
        return _channel.Writer.TryWrite(notification);
    }

    public IAsyncEnumerable<BasketLowStockNotification>
        ReadAllAsync(
            CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(
            cancellationToken);
    }
}