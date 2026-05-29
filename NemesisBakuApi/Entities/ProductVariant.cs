namespace NemesisBakuApi.Entities;

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid SizeId { get; set; }
    public Size Size { get; set; } = null!;

    public Guid ColorId { get; set; }
    public Color Color { get; set; } = null!;

    public int StockCount { get; set; }

    public bool IsActive { get; set; } = true;
}