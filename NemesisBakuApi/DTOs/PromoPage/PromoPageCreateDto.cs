using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.PromoPage;

public class PromoPageCreateDto
{
    public PromoPageType Type { get; set; }
    public IFormFile? File { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> ProductIds { get; set; } = new();
}
