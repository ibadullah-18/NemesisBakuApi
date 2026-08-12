using System.ComponentModel.DataAnnotations;

namespace NemesisBakuApi.Entities;

public class WhatsAppProductInquiry : BaseEntity
{
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [MaxLength(1000)]
    public string ProductLink { get; set; } = null!;

    [MaxLength(30)]
    public string SellerPhoneNumber { get; set; } = null!;

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(1000)]
    public string? UserAgent { get; set; }
}