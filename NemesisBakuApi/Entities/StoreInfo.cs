namespace NemesisBakuApi.Entities;

public class StoreInfo : BaseEntity
{
    public string StoreName { get; set; } = "NemesisBaku";
    public string? Description { get; set; }

    public string? PhoneNumber { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Email { get; set; }

    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? InstagramUrl { get; set; }
    public string? TikTokUrl { get; set; }

    public bool IsActive { get; set; } = true;
}