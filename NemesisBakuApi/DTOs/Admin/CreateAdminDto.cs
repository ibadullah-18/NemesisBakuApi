namespace NemesisBakuApi.DTOs.Admin;

public class CreateAdminDto
{
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }

    public string Password { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;
}