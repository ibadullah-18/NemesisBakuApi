namespace NemesisBakuApi.DTOs.Basket;

public class BasketSummaryDto
{
    public List<BasketItemDto> Items { get; set; } = new();

    public int TotalQuantity { get; set; }
    public decimal TotalPrice { get; set; }
}