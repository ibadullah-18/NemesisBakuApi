namespace NemesisBakuApi.Entities;

public class EmailAnnouncement : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;

    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }

    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    public Guid CreatedByUserId { get; set; }
}