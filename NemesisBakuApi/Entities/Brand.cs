namespace NemesisBakuApi.Entities;

public class Brand : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? ImageUrl { get; set; }


    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}