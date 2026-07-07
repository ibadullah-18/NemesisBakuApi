namespace NemesisBakuApi.DTOs.Product;

public class ProductDetailDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public string ProductCode { get; set; } = null!;
    public string? Model { get; set; }

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    public bool IsDiscounted { get; set; }
    public bool IsFeatured { get; set; }

    public string CategoryName { get; set; } = null!;
    public string BrandName { get; set; } = null!;

    public List<ProductImageDetailDto> Images { get; set; } = new();
    public List<ProductVariantDetailDto> Variants { get; set; } = new();
}

public class ProductImageDetailDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = null!;
    public bool IsMain { get; set; }
    public int DisplayOrder { get; set; }
}

public class ProductVariantDetailDto
{
    public Guid Id { get; set; }

    public Guid SizeId { get; set; }
    public string SizeValue { get; set; } = null!;

    public Guid ColorId { get; set; }
    public string ColorName { get; set; } = null!;
    public string? ColorHexCode { get; set; }

    public int StockCount { get; set; }
}