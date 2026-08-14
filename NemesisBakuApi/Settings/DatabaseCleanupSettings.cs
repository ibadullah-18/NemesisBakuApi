namespace NemesisBakuApi.Settings;

public sealed class DatabaseCleanupSettings
{
    public const string SectionName = "DatabaseCleanup";

    public bool Enabled { get; set; } = true;

    public int InitialDelaySeconds { get; set; } = 90;

    public int IntervalHours { get; set; } = 6;

    public int BatchSize { get; set; } = 500;

    public int MaxBatchesPerTable { get; set; } = 10;

    public int SiteVisitRetentionDays { get; set; } = 90;

    public int WhatsAppLogRetentionDays { get; set; } = 180;

    public int UserActivityRetentionDays { get; set; } = 180;

    public int AuditLogRetentionDays { get; set; } = 365;

    public int TelegramNotificationRetentionDays { get; set; } = 90;

    public int BasketLowStockLogRetentionDays { get; set; } = 180;

    public int EmailAnnouncementRetentionDays { get; set; } = 365;
}