namespace NemesisBakuApi.DTOs.Announcement;

public class CreateEmailAnnouncementDto
{
    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? ButtonText { get; set; }

    public string? ButtonUrl { get; set; }
}