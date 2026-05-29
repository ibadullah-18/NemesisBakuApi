namespace NemesisBakuApi.Entities;

public class Size : BaseEntity
{
    public string Value { get; set; } = null!;

    public ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}