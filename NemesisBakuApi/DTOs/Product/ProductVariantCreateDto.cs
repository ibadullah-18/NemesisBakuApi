namespace NemesisBakuApi.DTOs.Product;

public class ProductVariantCreateDto
{
    public Guid SizeId { get; set; }
    public Guid ColorId { get; set; }
    public int StockCount { get; set; }
}