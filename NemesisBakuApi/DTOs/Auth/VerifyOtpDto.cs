namespace NemesisBakuApi.DTOs.Auth;

public class VerifyOtpDto
{
    public string PhoneNumber { get; set; } = null!;
    public string Code { get; set; } = null!;
}