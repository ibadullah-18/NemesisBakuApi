namespace NemesisBakuApi.Entities;

public class HomeSection : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Subtitle { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<HomeSectionProduct> Products { get; set; } = new List<HomeSectionProduct>();
}