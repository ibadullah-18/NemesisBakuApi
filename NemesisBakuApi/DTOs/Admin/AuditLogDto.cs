namespace NemesisBakuApi.DTOs.Admin;

public class AuditLogDto
{
    public Guid Id { get; set; }

    public string? UserFullName { get; set; }

    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}