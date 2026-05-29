namespace NemesisBakuApi.DTOs.Profile;

public class VerifyChangePhoneDto
{
    public string NewPhoneNumber { get; set; } = null!;
    public string Code { get; set; } = null!;
}