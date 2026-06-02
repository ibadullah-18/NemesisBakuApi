namespace NemesisBakuApi.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}