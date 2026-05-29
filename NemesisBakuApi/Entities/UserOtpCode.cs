using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Entities;

public class UserOtpCode : BaseEntity
{
    public string PhoneNumber { get; set; } = null!;
    public string Code { get; set; } = null!;

    public OtpPurpose Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }
}