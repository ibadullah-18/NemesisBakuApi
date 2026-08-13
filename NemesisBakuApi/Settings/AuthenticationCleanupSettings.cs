namespace NemesisBakuApi.Settings;

public sealed class AuthenticationCleanupSettings
{
    public const string SectionName =
        "AuthenticationCleanup";

    public int OtpRetentionDays { get; set; } = 7;

    public int RefreshTokenRetentionDays
    { get; set; } = 30;

    public int IntervalHours { get; set; } = 6;

    public int InitialDelaySeconds { get; set; } = 60;
}