using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.PromoCode;

public class PromoCodeCreateDto
{
    public string Code { get; set; } = null!;

    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    public int? UsageLimit { get; set; }
    public decimal? MinOrderAmount { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}