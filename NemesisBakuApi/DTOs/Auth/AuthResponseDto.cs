namespace NemesisBakuApi.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}