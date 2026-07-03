using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly IEmailTemplateService _templateService;

    public EmailService(
        IOptions<EmailSettings> options,
        IEmailTemplateService templateService)
    {
        _settings = options.Value;
        _templateService = templateService;
    }

    public async Task<bool> SendOtpAsync(string email, string code)
    {
        var description = $@"
<p>Email təsdiq kodunuz:</p>
<h1 style='font-size:42px;letter-spacing:8px'>{code}</h1>
<p>Bu kod 5 dəqiqə ərzində keçərlidir.</p>";

        return await SendCustomAsync(
            EmailSenderType.Otp,
            email,
            "nemesisbaku təsdiq kodu",
            "Təsdiq kodunuz",
            description);
    }

    public async Task<bool> SendWelcomeAsync(string email, string fullName)
    {
        var description = $@"
<p>Salam, {fullName}.</p>
<p>NemesisBaku hesabınız uğurla yaradıldı.</p>
<p>Premium sneaker dünyasına xoş gəlmisiniz.</p>";

        return await SendCustomAsync(
            EmailSenderType.Info,
            email,
            "nemesisbaku-ya xoş gəlmisiniz",
            "Xoş gəldiniz",
            description,
            "Sayta keç",
            _settings.SiteUrl);
    }

    public async Task<bool> SendAnnouncementAsync(
        string email,
        string title,
        string description,
        string? buttonText,
        string? buttonUrl)
    {
        return await SendCustomAsync(
            EmailSenderType.Campaign,
            email,
            title,
            title,
            description,
            buttonText,
            buttonUrl);
    }

    public async Task<bool> SendBasketLowStockAsync(
        string email,
        string productName,
        string productLink,
        int stockCount)
    {
        var description = $@"
<p>Səbətinizdə olan məhsuldan artıq cəmi <b>{stockCount} ədəd</b> qalıb.</p>
<p><b>{productName}</b></p>
<p>Məhsul bitməmiş almağa tələsin.</p>";

        return await SendCustomAsync(
            EmailSenderType.Stock,
            email,
            "Səbətinizdəki məhsul azalır",
            "Məhsul az qalıb",
            description,
            "Məhsula bax",
            productLink);
    }

    public async Task<bool> SendCustomAsync(
        EmailSenderType senderType,
        string email,
        string subject,
        string title,
        string description,
        string? buttonText = null,
        string? buttonUrl = null)
    {
        var body = _templateService.Build(
            title,
            description,
            buttonText,
            buttonUrl);

        return await SendEmailAsync(senderType, email, subject, body);
    }

    private async Task<bool> SendEmailAsync(
        EmailSenderType senderType,
        string to,
        string subject,
        string body)
    {
        try
        {
            var account = _settings.Accounts
                .FirstOrDefault(x => x.Type == senderType);

            if (account == null)
                return false;

            using var client = new SmtpClient(account.Host, account.Port)
            {
                EnableSsl = account.EnableSsl,
                Credentials = new NetworkCredential(account.Username, account.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(account.FromEmail, account.FromName),
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

    public async Task<bool> SendOrderStatusAsync(
    string email,
    string fullName,
    string orderNumber,
    OrderStatus status,
    decimal totalPrice)
    {
        var title = status switch
        {
            OrderStatus.Confirmed => "Sifarişiniz qəbul olundu",
            OrderStatus.Preparing => "Sifarişiniz hazırlanır",
            OrderStatus.OnDelivery => "Sifarişiniz çatdırılmaya çıxdı",
            OrderStatus.Delivered => "Sifarişiniz təhvil verildi",
            OrderStatus.Cancelled => "Sifarişiniz ləğv edildi",
            OrderStatus.Rejected => "Sifarişiniz rədd edildi",
            _ => "Sifariş statusu yeniləndi"
        };

        var description = $@"
<p>Salam, {fullName}.</p>
<p><b>{orderNumber}</b> nömrəli sifarişinizin statusu yeniləndi.</p>
<p>Status: <b>{title}</b></p>
<p>Yekun məbləğ: <b>{totalPrice} AZN</b></p>";

        var body = _templateService.Build(
            title,
            description,
            "Sayta keç",
            _settings.SiteUrl);

        return await SendEmailAsync(
            EmailSenderType.Info,
            email,
            title,
            body);
    }
}