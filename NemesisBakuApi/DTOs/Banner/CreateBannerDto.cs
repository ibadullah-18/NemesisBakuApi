namespace NemesisBakuApi.DTOs.Banner;

public class CreateBannerDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }

    public int SortOrder { get; set; } = 0;

    public IFormFile File { get; set; } = null!;
}