using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Entities;

public class PromoPage : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public PromoPageType Type { get; set; }

    public int SlotNumber { get; set; }
    public string Slug { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PromoPageProduct> Products { get; set; } = new List<PromoPageProduct>();
}