using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class EmailService : IEmailService
{
    private const int SmtpTimeoutMilliseconds = 15000;

    private readonly EmailSettings _settings;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> options,
        IEmailTemplateService templateService,
        ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _templateService = templateService;
        _logger = logger;
    }

    public Task<bool> SendOtpAsync(
        string email,
        string code)
    {
        var description =
$@"<p>Email təsdiq kodunuz:</p>
<h1 style='font-size:42px;letter-spacing:8px'>
    {WebUtility.HtmlEncode(code)}
</h1>
<p>Bu kod 5 dəqiqə ərzində keçərlidir.</p>";

        return SendCustomAsync(
            EmailSenderType.Otp,
            email,
            "nemesisbaku təsdiq kodu",
            "Təsdiq kodunuz",
            description);
    }

    public Task<bool> SendWelcomeAsync(
        string email,
        string fullName)
    {
        var safeFullName =
            WebUtility.HtmlEncode(fullName);

        var description =
$@"<p>Salam, {safeFullName}.</p>
<p>NemesisBaku hesabınız uğurla yaradıldı.</p>
<p>Premium sneaker dünyasına xoş gəlmisiniz.</p>";

        return SendCustomAsync(
            EmailSenderType.Info,
            email,
            "nemesisbaku-ya xoş gəlmisiniz",
            "Xoş gəldiniz",
            description,
            "Sayta keç",
            _settings.SiteUrl);
    }

    public Task<bool> SendAnnouncementAsync(
        string email,
        string title,
        string description,
        string? buttonText,
        string? buttonUrl)
    {
        return SendCustomAsync(
            EmailSenderType.Campaign,
            email,
            title,
            title,
            description,
            buttonText,
            buttonUrl);
    }

    public Task<bool> SendBasketLowStockAsync(
        string email,
        string productName,
        string productLink,
        int stockCount)
    {
        var safeProductName =
            WebUtility.HtmlEncode(productName);

        var description =
$@"<p>Səbətinizdə olan məhsuldan artıq cəmi
<b>{stockCount} ədəd</b> qalıb.</p>
<p><b>{safeProductName}</b></p>
<p>Məhsul bitməmiş almağa tələsin.</p>";

        return SendCustomAsync(
            EmailSenderType.Stock,
            email,
            "Səbətinizdəki məhsul azalır",
            "Məhsul az qalıb",
            description,
            "Məhsula bax",
            productLink);
    }

    public Task<bool> SendOrderStatusAsync(
        string email,
        string fullName,
        string orderNumber,
        OrderStatus status,
        decimal totalPrice)
    {
        var title = status switch
        {
            OrderStatus.Confirmed =>
                "Sifarişiniz qəbul olundu",

            OrderStatus.Preparing =>
                "Sifarişiniz hazırlanır",

            OrderStatus.OnDelivery =>
                "Sifarişiniz çatdırılmaya çıxdı",

            OrderStatus.Delivered =>
                "Sifarişiniz təhvil verildi",

            OrderStatus.Cancelled =>
                "Sifarişiniz ləğv edildi",

            OrderStatus.Rejected =>
                "Sifarişiniz rədd edildi",

            _ => "Sifariş statusu yeniləndi"
        };

        var safeFullName =
            WebUtility.HtmlEncode(fullName);

        var safeOrderNumber =
            WebUtility.HtmlEncode(orderNumber);

        var description =
$@"<p>Salam, {safeFullName}.</p>
<p><b>{safeOrderNumber}</b> nömrəli sifarişinizin
statusu yeniləndi.</p>
<p>Status: <b>{WebUtility.HtmlEncode(title)}</b></p>
<p>Yekun məbləğ: <b>{totalPrice:0.00} AZN</b></p>";

        return SendCustomAsync(
            EmailSenderType.Info,
            email,
            title,
            title,
            description,
            "Sayta keç",
            _settings.SiteUrl);
    }

    private async Task<bool> SendCustomAsync(
        EmailSenderType senderType,
        string email,
        string subject,
        string title,
        string description,
        string? buttonText = null,
        string? buttonUrl = null)
    {
        if (!IsValidEmail(email))
        {
            _logger.LogWarning(
                "Email göndərilmədi: ünvan düzgün deyil.");

            return false;
        }

        var body = _templateService.Build(
            title,
            description,
            buttonText,
            buttonUrl);

        return await SendEmailAsync(
            senderType,
            email.Trim(),
            subject,
            body);
    }

    private async Task<bool> SendEmailAsync(
        EmailSenderType senderType,
        string to,
        string subject,
        string body)
    {
        var account = _settings.Accounts
            .FirstOrDefault(x =>
                x.Type == senderType);

        if (account == null)
        {
            _logger.LogError(
                "{SenderType} üçün email hesabı tapılmadı.",
                senderType);

            return false;
        }

        if (!IsAccountConfigured(account))
        {
            _logger.LogError(
                "{SenderType} email hesabının ayarları tam deyil.",
                senderType);

            return false;
        }

        try
        {
            using var client = new SmtpClient(
                account.Host,
                account.Port)
            {
                EnableSsl = account.EnableSsl,

                Credentials = new NetworkCredential(
                    account.Username,
                    account.Password),

                Timeout = SmtpTimeoutMilliseconds,
                DeliveryMethod =
                    SmtpDeliveryMethod.Network,

                UseDefaultCredentials = false
            };

            using var message = new MailMessage
            {
                From = new MailAddress(
                    account.FromEmail,
                    account.FromName),

                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(to);

            await client.SendMailAsync(message);

            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogWarning(
                ex,
                "Email SMTP tərəfindən göndərilmədi. " +
                "SenderType: {SenderType}",
                senderType);

            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Email konfiqurasiya xətasına görə " +
                "göndərilmədi. SenderType: {SenderType}",
                senderType);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Email göndərilərkən gözlənilməz xəta. " +
                "SenderType: {SenderType}",
                senderType);

            return false;
        }
    }

    private static bool IsAccountConfigured(
        EmailAccountSettings account)
    {
        return
            !string.IsNullOrWhiteSpace(account.Host) &&
            account.Port > 0 &&
            !string.IsNullOrWhiteSpace(
                account.FromEmail) &&
            !string.IsNullOrWhiteSpace(
                account.Username) &&
            !string.IsNullOrWhiteSpace(
                account.Password);
    }

    private static bool IsValidEmail(
        string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(
                email.Trim());

            return address.Address.Equals(
                email.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}