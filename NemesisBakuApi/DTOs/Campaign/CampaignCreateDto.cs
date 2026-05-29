namespace NemesisBakuApi.DTOs.Campaign;

public class CampaignCreateDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public string? ImageUrl { get; set; }
    public string? RedirectUrl { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}
