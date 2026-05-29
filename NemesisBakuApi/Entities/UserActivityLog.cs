namespace NemesisBakuApi.Entities;

public class UserActivityLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public string Action { get; set; } = null!;
    public string? Description { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}