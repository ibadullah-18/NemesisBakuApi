using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Services.Interfaces;

public interface IEmailService
{
    Task<bool> SendOtpAsync(string email, string code);
    Task<bool> SendWelcomeAsync(string email, string fullName);

    Task<bool> SendBasketLowStockAsync(
        string email,
        string productName,
        string productLink,
        int stockCount);

    Task<bool> SendOrderStatusAsync(
        string email,
        string fullName,
        string orderNumber,
        OrderStatus status,
        decimal totalPrice);

    Task<bool> SendAnnouncementAsync(
        string email,
        string title,
        string description,
        string? buttonText,
        string? buttonUrl);
}