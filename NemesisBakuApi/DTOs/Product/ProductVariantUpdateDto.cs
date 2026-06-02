namespace NemesisBakuApi.DTOs.Product;

public class ProductVariantUpdateDto
{
    public Guid SizeId { get; set; }
    public Guid ColorId { get; set; }
    public int StockCount { get; set; }
    public bool IsActive { get; set; } = true;
}