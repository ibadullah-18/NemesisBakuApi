namespace NemesisBakuApi.Entities;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Guid ProductVariantId { get; set; }

    public string ProductName { get; set; } = null!;
    public string ProductCode { get; set; } = null!;

    public string SizeValue { get; set; } = null!;
    public string ColorName { get; set; } = null!;

    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }

    public string? ProductImageUrl { get; set; }
    public string? ProductLink { get; set; }
}