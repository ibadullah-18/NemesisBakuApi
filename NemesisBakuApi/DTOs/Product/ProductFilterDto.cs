namespace NemesisBakuApi.DTOs.Product;

public class ProductFilterDto
{
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? SizeId { get; set; }
    public Guid? ColorId { get; set; }

    public string? Model { get; set; }
    public string? Search { get; set; }

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    public bool? IsDiscounted { get; set; }
    public bool? IsFeatured { get; set; }

    public string? Sort { get; set; } = "newest";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}