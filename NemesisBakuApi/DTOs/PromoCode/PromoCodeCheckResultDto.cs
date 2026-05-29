namespace NemesisBakuApi.DTOs.PromoCode;

public class PromoCodeCheckResultDto
{
    public bool IsValid { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Message { get; set; } = null!;
}