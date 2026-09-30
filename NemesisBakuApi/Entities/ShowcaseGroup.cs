using System.ComponentModel.DataAnnotations;

namespace NemesisBakuApi.Entities;

public class ShowcaseGroup : BaseEntity
{
    [MaxLength(120)] public string Name { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
    public List<ShowcaseBlock> Blocks { get; set; } = [];
}

public class ShowcaseBlock : BaseEntity
{
    public Guid ShowcaseGroupId { get; set; }
    public ShowcaseGroup Group { get; set; } = null!;
    public int DisplayOrder { get; set; }
    [MaxLength(16)] public string Shape { get; set; } = "square";
    [MaxLength(16)] public string TargetType { get; set; } = "none";
    [MaxLength(100)] public string? Slug { get; set; }
    [MaxLength(2048)] public string? ExternalUrl { get; set; }
    [MaxLength(2048)] public string ImageUrl { get; set; } = "";
    [MaxLength(2048)] public string MobileImageUrl { get; set; } = "";
    [MaxLength(200)] public string? ImageAlt { get; set; }
    [MaxLength(200)] public string? Title { get; set; }
    [MaxLength(500)] public string? Subtitle { get; set; }
    [MaxLength(20000)] public string? Description { get; set; }
    [MaxLength(20000)] public string? AfterProductsDescription { get; set; }
    public List<ShowcaseBlockProduct> Products { get; set; } = [];
}

public class ShowcaseBlockProduct
{
    public Guid ShowcaseBlockId { get; set; }
    public ShowcaseBlock Block { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int DisplayOrder { get; set; }
}
