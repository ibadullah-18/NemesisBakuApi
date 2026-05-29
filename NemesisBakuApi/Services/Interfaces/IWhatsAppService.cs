namespace NemesisBakuApi.Services.Interfaces;

public interface IWhatsAppService
{
    Task<bool> SendOtpAsync(string phoneNumber, string code);
    Task<bool> SendOrderNotificationAsync(string message);
}