namespace NemesisBakuApi.Entities;

public class PromoCodeUsage : BaseEntity
{
    public Guid PromoCodeId { get; set; }
    public PromoCode PromoCode { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public decimal DiscountAmount { get; set; }
}