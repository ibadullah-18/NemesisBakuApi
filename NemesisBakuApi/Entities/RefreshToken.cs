using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NemesisBakuApi.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [Column("Token")]
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }
        = Array.Empty<byte>();
}