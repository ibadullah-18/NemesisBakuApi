using NemesisBakuApi.DTOs.Product;

public class ProductCreateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public string ProductCode { get; set; } = null!;
    public string? Model { get; set; }

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    public bool IsDiscounted { get; set; }
    public bool IsFeatured { get; set; }

    public Guid CategoryId { get; set; }
    public Guid BrandId { get; set; }

    public List<ProductVariantCreateDto> Variants { get; set; } = new();
}