namespace NemesisBakuApi.DTOs.Profile;

public class UserAddressCreateDto
{
    public string? Title { get; set; }

    public string AddressText { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    public string? BuildingNumber { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? Note { get; set; }

    public bool IsDefault { get; set; } = false;
}