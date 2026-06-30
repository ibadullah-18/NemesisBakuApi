namespace NemesisBakuApi.DTOs.Store;

public class StoreInfoUpdateDto
{
    public string StoreName { get; set; } = null!;
    public string? Slogan { get; set; }

    public IFormFile? LogoFile { get; set; }

    public string? AboutTitle { get; set; }
    public string? AboutContent { get; set; }

    public string? MissionContent { get; set; }
    public string? VisionContent { get; set; }
    public string? WhyChooseUsContent { get; set; }

    public string? ReturnPolicyTitle { get; set; }
    public string? ReturnPolicyContent { get; set; }
    public string? ExchangePolicyContent { get; set; }
    public string? ReturnExceptionsContent { get; set; }
    public string? ReturnProcessContent { get; set; }

    public string? DeliveryTitle { get; set; }
    public string? DeliveryContent { get; set; }
    public string? DeliveryBakuText { get; set; }
    public string? DeliveryAbsheronSumgaitText { get; set; }
    public string? DeliveryRegionsText { get; set; }
    public string? PaymentAndCheckText { get; set; }

    public string? PhoneNumber { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }

    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? WorkingHours { get; set; }

    public string? InstagramUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? FacebookUrl { get; set; }
}