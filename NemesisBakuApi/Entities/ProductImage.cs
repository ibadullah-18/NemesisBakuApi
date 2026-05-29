namespace NemesisBakuApi.Entities;

public class ProductImage : BaseEntity
{
    public string ImageUrl { get; set; } = null!;
    public bool IsMain { get; set; } = false;
    public int Order { get; set; } = 0;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}