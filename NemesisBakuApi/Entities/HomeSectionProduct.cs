namespace NemesisBakuApi.Entities;

public class HomeSectionProduct : BaseEntity
{
    public Guid HomeSectionId { get; set; }
    public HomeSection HomeSection { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Order { get; set; } = 0;
}