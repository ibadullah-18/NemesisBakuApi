using NemesisBakuApi.Enums;
using System.Text.Json.Serialization;

namespace NemesisBakuApi.DTOs.PromoPage;

public class PromoPageDto
{
    public Guid Id { get; set; }
    public PromoPageType Type { get; set; }
    public string? ImageUrl { get; set; }
    public string MobileImageUrl { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> ProductIds { get; set; } = new();

    // Keçid dövründə köhnə controller-lərin compile olunması üçün saxlanılıb,
    // amma JSON cavabına və yeni admin interfeysinə çıxmır.
    [JsonIgnore]
    public string? Title { get; set; }

    [JsonIgnore]
    public string? Description { get; set; }

    [JsonIgnore]
    public DateTime EndDate { get; set; }
}
