namespace NemesisBakuApi.DTOs.HomeSection;

public class HomeSectionProductDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string ProductCode { get; set; } = null!;

    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }

    public bool IsDiscounted { get; set; }

    public string? ImageUrl { get; set; }
}