namespace NemesisBakuApi.DTOs.Profile;

public class UserAddressDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;
    public string AddressText { get; set; } = null!;

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    public string? BuildingNumber { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? Note { get; set; }

    public bool IsDefault { get; set; }
}