namespace NemesisBakuApi.Entities;

public class PromoPageProduct : BaseEntity
{
    public Guid PromoPageId { get; set; }
    public PromoPage PromoPage { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Order { get; set; }
}