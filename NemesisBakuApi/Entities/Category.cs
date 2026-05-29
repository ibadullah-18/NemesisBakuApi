namespace NemesisBakuApi.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? IconUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}