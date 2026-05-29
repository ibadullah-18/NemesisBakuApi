namespace NemesisBakuApi.Entities;

public class WhatsAppClickLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string? PageUrl { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public string ClickType { get; set; } = null!;
}