namespace NemesisBakuApi.DTOs.Stats;

public class TrackVisitDto
{
    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string? EventId { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string? SessionId { get; set; }
    public string VisitorId { get; set; } = null!;
    public string? PageUrl { get; set; }
}
