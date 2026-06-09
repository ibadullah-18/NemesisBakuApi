namespace NemesisBakuApi.DTOs.Product;

public class ProductFilterOptionsDto
{
    public List<FilterOptionDto> Categories { get; set; } = new();
    public List<FilterOptionDto> Brands { get; set; } = new();
    public List<FilterOptionDto> Sizes { get; set; } = new();
    public List<ColorFilterOptionDto> Colors { get; set; } = new();

    public List<string> Models { get; set; } = new();

    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
}

public class FilterOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? ImageUrl { get; set; }
}

public class ColorFilterOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? HexCode { get; set; }
}