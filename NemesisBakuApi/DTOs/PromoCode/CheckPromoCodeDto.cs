namespace NemesisBakuApi.DTOs.PromoCode;

public class CheckPromoCodeDto
{
    public string Code { get; set; } = null!;
    public decimal OrderAmount { get; set; }
}