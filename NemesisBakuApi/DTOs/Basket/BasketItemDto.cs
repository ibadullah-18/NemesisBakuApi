namespace NemesisBakuApi.DTOs.Basket;

public class BasketItemDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }
    public Guid ProductVariantId { get; set; }

    public string ProductName { get; set; } = null!;
    public string ProductCode { get; set; } = null!;
    public string? ProductImageUrl { get; set; }

    public string SizeValue { get; set; } = null!;
    public string ColorName { get; set; } = null!;
    public string? ColorHexCode { get; set; }

    public decimal OriginalPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }

    public int Quantity { get; set; }

    public decimal OriginalTotalPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public bool HasDiscount { get; set; }

    public int StockCount { get; set; }
}