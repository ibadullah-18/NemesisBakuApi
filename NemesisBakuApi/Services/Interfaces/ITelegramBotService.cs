namespace NemesisBakuApi.Services.Interfaces;

public interface ITelegramBotService
{
    bool IsConfigured { get; }

    Task ConfigureWebhookAsync(CancellationToken cancellationToken = default);

    Task SendContactRequestAsync(
        long chatId,
        CancellationToken cancellationToken = default);

    Task SendTextAsync(
        long chatId,
        string text,
        bool removeKeyboard = false,
        CancellationToken cancellationToken = default);

    Task SendNewOrderAsync(
        long chatId,
        string adminFullName,
        string panelRole,
        Guid orderId,
        string customerFullName,
        int productCount,
        decimal totalPrice,
        CancellationToken cancellationToken = default);
}
