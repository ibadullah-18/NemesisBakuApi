namespace NemesisBakuApi.Entities;

public class TelegramOrderNotification : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid AdminUserId { get; set; }
    public AppUser AdminUser { get; set; } = null!;

    public long TelegramChatId { get; set; }
    public string AdminFullName { get; set; } = null!;
    public string PanelRole { get; set; } = null!;

    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}
