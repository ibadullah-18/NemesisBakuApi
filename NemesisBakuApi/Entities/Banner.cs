namespace NemesisBakuApi.Entities;

public class Banner : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; } = 0;
}