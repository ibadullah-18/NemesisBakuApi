namespace NemesisBakuApi.DTOs.Auth;

public class ResetPasswordWithOtpDto
{
    public string Email { get; set; } = null!;
    public string Code { get; set; } = null!;

    public string NewPassword { get; set; } = null!;
    public string ConfirmNewPassword { get; set; } = null!;
}