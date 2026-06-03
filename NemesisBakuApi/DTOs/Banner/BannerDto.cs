namespace NemesisBakuApi.DTOs.Banner;

public class BannerDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }

    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}