using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Settings;

public class EmailAccountSettings
{
    public EmailSenderType Type { get; set; }

    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public bool EnableSsl { get; set; } = true;

    public string FromEmail { get; set; } = null!;
    public string FromName { get; set; } = "nemesisbaku";

    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}