using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.PromoPage;

public class PromoPageDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public PromoPageType Type { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; }

    public List<Guid> ProductIds { get; set; } = new();
}