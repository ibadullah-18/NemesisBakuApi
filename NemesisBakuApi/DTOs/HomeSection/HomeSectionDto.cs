namespace NemesisBakuApi.DTOs.HomeSection;

public class HomeSectionDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Subtitle { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public List<Guid> ProductIds { get; set; } = new();
}