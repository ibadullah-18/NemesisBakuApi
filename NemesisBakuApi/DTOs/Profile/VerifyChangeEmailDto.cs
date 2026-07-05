namespace NemesisBakuApi.DTOs.Profile;

public class VerifyChangeEmailDto
{
    public string NewEmail { get; set; } = null!;
    public string Code { get; set; } = null!;
}