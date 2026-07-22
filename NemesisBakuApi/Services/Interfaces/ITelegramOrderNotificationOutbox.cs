using NemesisBakuApi.Entities;

namespace NemesisBakuApi.Services.Interfaces;

public interface ITelegramOrderNotificationOutbox
{
    Task EnqueueAsync(
        Order order,
        CancellationToken cancellationToken = default);
}
