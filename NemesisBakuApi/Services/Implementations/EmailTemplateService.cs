using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly EmailSettings _settings;

    public EmailTemplateService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public string Build(
        string title,
        string description,
        string? buttonText = null,
        string? buttonUrl = null)
    {
        var url = string.IsNullOrWhiteSpace(buttonUrl)
            ? _settings.SiteUrl
            : buttonUrl;

        var btn = string.IsNullOrWhiteSpace(buttonText)
            ? "Sayta keç"
            : buttonText;

        return $@"
<div style='background:#f4f4f4;padding:32px;font-family:Arial,sans-serif'>
  <div style='max-width:640px;margin:auto;background:#fff;border-radius:22px;padding:34px;text-align:center'>
    <a href='{_settings.SiteUrl}'>
      <img src='{_settings.LogoUrl}' style='max-width:260px;margin-bottom:28px' />
    </a>

    <h1 style='font-size:28px;color:#111;margin:0 0 18px'>{title}</h1>

    <div style='font-size:16px;line-height:1.7;color:#333'>
      {description}
    </div>

    <a href='{url}' style='display:inline-block;margin-top:28px;background:#111;color:#fff;text-decoration:none;padding:14px 28px;border-radius:999px;font-weight:bold'>
      {btn}
    </a>

    <hr style='border:none;border-top:1px solid #eee;margin:34px 0' />

    <div style='font-size:14px'>
      <a href='{_settings.InstagramUrl}' style='color:#111;margin:0 8px'>Instagram</a>
      <a href='{_settings.TikTokUrl}' style='color:#111;margin:0 8px'>TikTok</a>
      <a href='{_settings.WhatsAppUrl}' style='color:#111;margin:0 8px'>WhatsApp</a>
    </div>

    <p style='font-size:12px;color:#999;margin-top:24px'>
      © nemesisbaku. Bu email avtomatik göndərilmişdir.
    </p>
  </div>
</div>";
    }
}