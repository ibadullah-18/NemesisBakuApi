namespace NemesisBakuApi.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public string ProductCode { get; set; } = null!;
    public string? Model { get; set; }

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    public bool IsDiscounted { get; set; } = false;
    public bool IsFeatured { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public int ViewCount { get; set; } = 0;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<BasketItem> BasketItems { get; set; } = new List<BasketItem>();
}