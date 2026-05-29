namespace NemesisBakuApi.DTOs.Profile;

public class ProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? LoyaltyCardCode { get; set; }
}