namespace NemesisBakuApi.Entities;

public class SiteVisit : BaseEntity
{
    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string? EventId { get; set; }
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public string VisitorId { get; set; } = null!;

    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? PageUrl { get; set; }
    public string? UserAgent { get; set; }

    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
}
