using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task<bool> SendOtpAsync(string email, string code)
    {
        var subject = "nemesisbaku təsdiq kodu";

        var body = $@"
            <h2>nemesisbaku</h2>
            <p>Təsdiq kodunuz:</p>
            <h1>{code}</h1>
            <p>Bu kod 5 dəqiqə ərzində keçərlidir.</p>
        ";

        return await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendWelcomeAsync(string email, string fullName)
    {
        var subject = "nemesisbaku-ya xoş gəlmisiniz";

        var body = $@"
            <h2>Salam, {fullName}</h2>
            <p>nemesisbaku hesabınız uğurla yaradıldı.</p>
            <p>Premium sneaker dünyasına xoş gəlmisiniz.</p>
        ";

        return await SendEmailAsync(email, subject, body);
    }

    private async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(to);

            await client.SendMailAsync(message);

            return true;
        }
        catch
        {
            return false;
        }
    }
}