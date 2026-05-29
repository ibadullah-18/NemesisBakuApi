namespace NemesisBakuApi.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }

    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
}