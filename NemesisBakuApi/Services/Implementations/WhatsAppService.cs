using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppSettings _settings;

    public WhatsAppService(
        HttpClient httpClient,
        IOptions<WhatsAppSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<bool> SendOtpAsync(string phoneNumber, string code)
    {
        var message = $"NemesisBaku təsdiq kodunuz: {code}";

        return await SendTextMessageAsync(phoneNumber, message);
    }

    public async Task<bool> SendOrderNotificationAsync(string message)
    {
        return await SendTextMessageAsync(_settings.SellerPhoneNumber, message);
    }

    public async Task<bool> SendTextMessageAsync(string toPhoneNumber, string message)
    {
        var url =
            $"https://graph.facebook.com/{_settings.ApiVersion}/{_settings.PhoneNumberId}/messages";

        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "text",
            text = new
            {
                body = message
            }
        };

        var json = JsonSerializer.Serialize(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);

        var responseContent = await response.Content.ReadAsStringAsync();

        Console.WriteLine(responseContent);

        return response.IsSuccessStatusCode;
    }
}