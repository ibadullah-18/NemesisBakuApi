namespace NemesisBakuApi.DTOs.Common;

public class BrandCreateDto
{
    public string Name { get; set; } = null!;
    public IFormFile? Image { get; set; }
}