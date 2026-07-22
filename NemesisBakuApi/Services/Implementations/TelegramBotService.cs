using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class TelegramBotService : ITelegramBotService
{
    private readonly HttpClient _httpClient;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(
        HttpClient httpClient,
        IOptions<TelegramSettings> options,
        ILogger<TelegramBotService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _settings.Enabled &&
        !string.IsNullOrWhiteSpace(_settings.BotToken) &&
        !string.IsNullOrWhiteSpace(_settings.WebhookSecret);

    public Task ConfigureWebhookAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var webhookUrl =
            $"{_settings.PublicBaseUrl.TrimEnd('/')}/api/telegram/webhook";

        return CallApiAsync(
            "setWebhook",
            new
            {
                url = webhookUrl,
                secret_token = _settings.WebhookSecret,
                allowed_updates = new[] { "message" },
                drop_pending_updates = false
            },
            cancellationToken);
    }

    public Task SendContactRequestAsync(
        long chatId,
        CancellationToken cancellationToken = default)
    {
        return CallApiAsync(
            "sendMessage",
            new
            {
                chat_id = chatId,
                text =
                    "Salam. Telegram bildirişlərini admin hesabınıza bağlamaq üçün " +
                    "aşağıdakı düymə ilə öz telefon nömrənizi paylaşın.",
                reply_markup = new
                {
                    keyboard = new[]
                    {
                        new[]
                        {
                            new
                            {
                                text = "Telefon nömrəmi paylaş",
                                request_contact = true
                            }
                        }
                    },
                    resize_keyboard = true,
                    one_time_keyboard = true
                }
            },
            cancellationToken);
    }

    public Task SendTextAsync(
        long chatId,
        string text,
        bool removeKeyboard = false,
        CancellationToken cancellationToken = default)
    {
        if (!removeKeyboard)
        {
            return CallApiAsync(
                "sendMessage",
                new
                {
                    chat_id = chatId,
                    text
                },
                cancellationToken);
        }

        return CallApiAsync(
            "sendMessage",
            new
            {
                chat_id = chatId,
                text,
                reply_markup = new { remove_keyboard = true }
            },
            cancellationToken);
    }

    public Task SendNewOrderAsync(
        long chatId,
        string adminFullName,
        string panelRole,
        Guid orderId,
        string customerFullName,
        int productCount,
        decimal totalPrice,
        CancellationToken cancellationToken = default)
    {
        var panelSegment = panelRole.Equals(
            "SuperAdmin",
            StringComparison.OrdinalIgnoreCase)
                ? "SuperAdmin"
                : "Admin";

        var orderUrl =
            $"{_settings.SiteBaseUrl.TrimEnd('/')}/{panelSegment}/orders/{orderId}";

        var message =
            $"Salam, <b>{EscapeHtml(adminFullName)}</b>\n\n" +
            "<b>Yeni sifariş daxil oldu.</b>\n\n" +
            $"Müştəri: {EscapeHtml(customerFullName)}\n" +
            $"Məhsul sayı: {productCount}\n" +
            $"Ümumi məbləğ: {totalPrice:0.00} ₼";

        return CallApiAsync(
            "sendMessage",
            new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "HTML",
                disable_web_page_preview = true,
                reply_markup = new
                {
                    inline_keyboard = new[]
                    {
                        new[]
                        {
                            new
                            {
                                text = "Sifarişi aç",
                                url = orderUrl
                            }
                        }
                    }
                }
            },
            cancellationToken);
    }

    private async Task CallApiAsync(
        string method,
        object payload,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var requestUrl =
            $"https://api.telegram.org/bot{_settings.BotToken}/{method}";

        using var response = await _httpClient.PostAsJsonAsync(
            requestUrl,
            payload,
            cancellationToken);

        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        _logger.LogWarning(
            "Telegram API {Method} çağırışı uğursuz oldu. Status: {StatusCode}. Cavab: {Response}",
            method,
            (int)response.StatusCode,
            Limit(responseBody, 500));

        throw new HttpRequestException(
            $"Telegram API xətası: {(int)response.StatusCode}",
            null,
            response.StatusCode);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Telegram BotToken və WebhookSecret konfiqurasiya edilməyib.");
        }
    }

    private static string EscapeHtml(string value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string Limit(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
