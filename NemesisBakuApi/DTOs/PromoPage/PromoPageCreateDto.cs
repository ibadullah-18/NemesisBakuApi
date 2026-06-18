using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.PromoPage;

public class PromoPageCreateDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public PromoPageType Type { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public IFormFile? File { get; set; }

    public List<Guid> ProductIds { get; set; } = new();
}