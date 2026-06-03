namespace NemesisBakuApi.DTOs.Banner;

public class UpdateBannerDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }

    public bool IsActive { get; set; }
    public int SortOrder { get; set; }

    public IFormFile? File { get; set; }
}