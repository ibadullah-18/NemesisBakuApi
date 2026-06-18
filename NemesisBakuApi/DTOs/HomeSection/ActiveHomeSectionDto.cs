namespace NemesisBakuApi.DTOs.HomeSection;

public class ActiveHomeSectionDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Subtitle { get; set; }

    public int DisplayOrder { get; set; }

    public List<HomeSectionProductDto> Products { get; set; } = new();
}