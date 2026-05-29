namespace NemesisBakuApi.Entities;

public class WhatsAppProductInquiry : BaseEntity
{
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string ProductLink { get; set; } = null!;
    public string SellerPhoneNumber { get; set; } = null!;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}