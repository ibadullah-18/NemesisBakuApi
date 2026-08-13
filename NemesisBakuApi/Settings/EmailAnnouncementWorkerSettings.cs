namespace NemesisBakuApi.Settings;

public sealed class EmailAnnouncementWorkerSettings
{
    public const string SectionName = "EmailAnnouncementWorker";

    public int MaxRecipients { get; set; } = 1000;

    public int BatchSize { get; set; } = 10;

    public int PollIntervalSeconds { get; set; } = 10;

    public int InitialDelaySeconds { get; set; } = 10;

    public int HistoryLimit { get; set; } = 100;
}