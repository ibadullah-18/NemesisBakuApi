namespace NemesisBakuApi.DTOs.Store;

public class StoreInfoPatchDto
{
    public string? StoreName { get; set; }
    public string? Slogan { get; set; }
    public string? LogoUrl { get; set; }

    public string? AboutTitle { get; set; }
    public string? AboutContent { get; set; }

    public string? MissionContent { get; set; }
    public string? VisionContent { get; set; }
    public string? WhyChooseUsContent { get; set; }

    public string? ReturnPolicyTitle { get; set; }
    public string? ReturnPolicyContent { get; set; }

    public string? PhoneNumber { get; set; }
    public string? WhatsAppNumber { get; set; }

    public string? Address { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? WorkingHours { get; set; }

    public string? InstagramUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? FacebookUrl { get; set; }
}