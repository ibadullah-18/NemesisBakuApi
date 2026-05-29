namespace NemesisBakuApi.Entities;

public class Color : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? HexCode { get; set; }

    public ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}