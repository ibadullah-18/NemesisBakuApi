using System.ComponentModel.DataAnnotations;

namespace NemesisBakuApi.DTOs.Showcase;

public class ShowcaseOrderInput
{
    public Guid Id { get; set; }
    public Guid Version { get; set; }
}

public class ShowcaseGroupInput
{
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Range(1, 10000)] public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; }
    public Guid? Version { get; set; }
    [MinLength(1), MaxLength(3)] public List<ShowcaseBlockInput> Blocks { get; set; } = [];
}

public class ShowcaseBlockInput
{
    public Guid? Id { get; set; }
    [Required, RegularExpression("^(square|portrait|landscape)$")]
    public string Shape { get; set; } = "square";
    [Required, RegularExpression("^(none|internal|external)$")]
    public string TargetType { get; set; } = "none";
    [MaxLength(101)] public string? Slug { get; set; }
    [MaxLength(2048)] public string? ExternalUrl { get; set; }
    [MaxLength(200)] public string? ImageAlt { get; set; }
    [MaxLength(200)] public string? Title { get; set; }
    [MaxLength(500)] public string? Subtitle { get; set; }
    [MaxLength(20000)] public string? Description { get; set; }
    [MaxLength(20000)] public string? AfterProductsDescription { get; set; }
    [MaxLength(200)] public List<Guid> ProductIds { get; set; } = [];
    public IFormFile? File { get; set; }
    public IFormFile? MobileFile { get; set; }
}
