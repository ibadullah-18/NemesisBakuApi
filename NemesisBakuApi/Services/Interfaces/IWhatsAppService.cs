namespace NemesisBakuApi.Services.Interfaces;

public interface IWhatsAppService
{
    Task<bool> SendOrderNotificationAsync(string phoneNumber, string message);
    Task<bool> SendTextMessageAsync(string phoneNumber, string message);

    Task<bool> SendLowStockNotificationAsync(
        string sellerPhoneNumber,
        string productName,
        string size,
        string color,
        int stockCount);
}