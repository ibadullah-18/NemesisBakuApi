using System.ComponentModel.DataAnnotations;
using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Entities;

public class PromoCode : BaseEntity
{
    public string Code { get; set; } = null!;

    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }

    public decimal? MinOrderAmount { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PromoCodeUsage> Usages { get; set; }
        = new List<PromoCodeUsage>();
}