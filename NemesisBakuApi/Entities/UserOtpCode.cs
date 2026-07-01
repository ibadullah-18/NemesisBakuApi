using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;

public class UserOtpCode : BaseEntity
{
    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string Code { get; set; } = null!;

    public OtpPurpose Purpose { get; set; }

    public bool IsUsed { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }
}