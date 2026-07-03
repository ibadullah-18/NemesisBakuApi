namespace NemesisBakuApi.Entities;

public class CourierPhone : BaseEntity
{
    public string Title { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public bool IsDefault { get; set; } = false;
}