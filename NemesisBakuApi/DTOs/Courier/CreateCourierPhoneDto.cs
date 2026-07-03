namespace NemesisBakuApi.DTOs.Courier;

public class CreateCourierPhoneDto
{
    public string Title { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public bool IsDefault { get; set; } = false;
}   