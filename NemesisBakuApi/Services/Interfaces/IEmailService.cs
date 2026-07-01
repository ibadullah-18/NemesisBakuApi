namespace NemesisBakuApi.Services.Interfaces;

public interface IEmailService
{
    Task<bool> SendOtpAsync(string email, string code);
    Task<bool> SendWelcomeAsync(string email, string fullName);
}