namespace NemesisBakuApi.DTOs.Auth;

public class VerifyRegisterOtpDto
{
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public string Password { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;

    public string? LoyaltyCardCode { get; set; }

    public string Code { get; set; } = null!;
}