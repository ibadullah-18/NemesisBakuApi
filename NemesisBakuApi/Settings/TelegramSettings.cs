namespace NemesisBakuApi.Settings;

public class TelegramSettings
{
    public const string SectionName = "Telegram";

    public bool Enabled { get; set; } = true;
    public string BotToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BotUsername { get; set; } = "nemesisbakuSifarisBot";
    public string PublicBaseUrl { get; set; } = "https://nemesisbaku.az";
    public string SiteBaseUrl { get; set; } = "https://nemesisbaku.az";
}
