namespace NemesisBakuApi.DTOs.Product;

public class ProductListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ProductCode { get; set; } = null!;
    public string? Model { get; set; }

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    public bool IsDiscounted { get; set; }
    public bool IsFeatured { get; set; }

    public string CategoryName { get; set; } = null!;
    public string BrandName { get; set; } = null!;

    public string? MainImageUrl { get; set; }

    public int TotalStock { get; set; }
}