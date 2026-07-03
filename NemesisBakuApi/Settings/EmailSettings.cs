namespace NemesisBakuApi.Settings;

public class EmailSettings
{
    public string SiteUrl { get; set; } = "https://nemesisbaku.az";
    public string LogoUrl { get; set; } = null!;

    public string InstagramUrl { get; set; } = null!;
    public string TikTokUrl { get; set; } = null!;
    public string WhatsAppUrl { get; set; } = null!;

    public List<EmailAccountSettings> Accounts { get; set; } = new();
}