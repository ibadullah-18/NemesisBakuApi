namespace NemesisBakuApi.DTOs.Product;

public class ProductUpdateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public string ProductCode { get; set; } = null!;
    public string? Model { get; set; }

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    public bool IsDiscounted { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }

    public Guid CategoryId { get; set; }
    public Guid BrandId { get; set; }
}