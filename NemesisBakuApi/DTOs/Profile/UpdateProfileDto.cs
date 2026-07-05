namespace NemesisBakuApi.DTOs.Profile;

public class UpdateProfileDto
{
    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? LoyaltyCardCode { get; set; }

    public IFormFile? ProfileImage { get; set; }
}