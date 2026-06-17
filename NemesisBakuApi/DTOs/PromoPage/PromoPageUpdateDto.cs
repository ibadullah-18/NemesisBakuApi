namespace NemesisBakuApi.DTOs.PromoPage;

public class PromoPageUpdateDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public List<Guid> ProductIds { get; set; } = new();
}