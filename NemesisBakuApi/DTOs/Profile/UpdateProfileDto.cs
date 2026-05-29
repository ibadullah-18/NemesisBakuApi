namespace NemesisBakuApi.DTOs.Profile;

public class UpdateProfileDto
{
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? LoyaltyCardCode { get; set; }
}