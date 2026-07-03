namespace NemesisBakuApi.DTOs.Courier;

public class CourierPhoneDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public bool IsDefault { get; set; }
}