namespace NemesisBakuApi.Entities;

public class BasketLowStockEmailLog : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Guid ProductVariantId { get; set; }

    public string Email { get; set; } = null!;

    public int StockCountAtSend { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}