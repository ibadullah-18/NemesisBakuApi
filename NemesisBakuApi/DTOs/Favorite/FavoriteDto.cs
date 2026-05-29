namespace NemesisBakuApi.DTOs.Favorite;

public class FavoriteDto
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = null!;
    public string ProductCode { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    public bool IsDiscounted { get; set; }

    public string? MainImageUrl { get; set; }
}