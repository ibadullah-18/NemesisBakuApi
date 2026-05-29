namespace NemesisBakuApi.DTOs.Basket;

public class AddToBasketDto
{
    public Guid ProductId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; } = 1;
}
