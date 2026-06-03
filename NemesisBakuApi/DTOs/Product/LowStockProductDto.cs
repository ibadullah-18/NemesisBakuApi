namespace NemesisBakuApi.DTOs.Product;

public class LowStockProductDto
{
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }

    public string ProductName { get; set; } = null!;
    public string ProductCode { get; set; } = null!;

    public string SizeValue { get; set; } = null!;
    public string ColorName { get; set; } = null!;

    public int StockCount { get; set; }

    public string? MainImageUrl { get; set; }
}