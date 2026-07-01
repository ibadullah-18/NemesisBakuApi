namespace NemesisBakuApi.DTOs.Auth;

public class LoginDto
{
    public string EmailOrPhoneNumber { get; set; } = null!;
    public string Password { get; set; } = null!;
}