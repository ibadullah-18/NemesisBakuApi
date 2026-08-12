using System.Net;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class EmailTemplateService
    : IEmailTemplateService
{
    private readonly EmailSettings _settings;

    public EmailTemplateService(
        IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public string Build(
        string title,
        string description,
        string? buttonText = null,
        string? buttonUrl = null)
    {
        var siteUrl = GetSafeUrl(
            _settings.SiteUrl,
            "https://nemesisbaku.az");

        var logoUrl = GetSafeUrl(
            _settings.LogoUrl,
            string.Empty);

        var targetUrl = GetSafeUrl(
            buttonUrl,
            siteUrl);

        var instagramUrl = GetSafeUrl(
            _settings.InstagramUrl,
            siteUrl);

        var tikTokUrl = GetSafeUrl(
            _settings.TikTokUrl,
            siteUrl);

        var whatsAppUrl = GetSafeUrl(
            _settings.WhatsAppUrl,
            siteUrl);

        var safeTitle =
            WebUtility.HtmlEncode(title);

        var safeButtonText =
            WebUtility.HtmlEncode(
                string.IsNullOrWhiteSpace(buttonText)
                    ? "Sayta keç"
                    : buttonText.Trim());

        var logoHtml =
            string.IsNullOrWhiteSpace(logoUrl)
                ? string.Empty
                : $@"
      <img
        src='{WebUtility.HtmlEncode(logoUrl)}'
        alt='nemesisbaku'
        style='max-width:260px;margin-bottom:28px'
      />";

        return
$@"<!DOCTYPE html>
<html lang='az'>
<head>
  <meta charset='utf-8' />
  <meta
    name='viewport'
    content='width=device-width,initial-scale=1'
  />
  <title>{safeTitle}</title>
</head>
<body style='margin:0;padding:0;background:#f4f4f4'>
  <div
    style='background:#f4f4f4;padding:32px 16px;
    font-family:Arial,sans-serif'
  >
    <div
      style='max-width:640px;margin:auto;background:#fff;
      border-radius:22px;padding:34px;text-align:center'
    >
      <a href='{WebUtility.HtmlEncode(siteUrl)}'>
        {logoHtml}
      </a>

      <h1
        style='font-size:28px;color:#111;margin:0 0 18px'
      >
        {safeTitle}
      </h1>

      <div
        style='font-size:16px;line-height:1.7;color:#333'
      >
        {description}
      </div>

      <a
        href='{WebUtility.HtmlEncode(targetUrl)}'
        style='display:inline-block;margin-top:28px;
        background:#111;color:#fff;text-decoration:none;
        padding:14px 28px;border-radius:999px;
        font-weight:bold'
      >
        {safeButtonText}
      </a>

      <hr
        style='border:none;border-top:1px solid #eee;
        margin:34px 0'
      />

      <div style='font-size:14px'>
        <a
          href='{WebUtility.HtmlEncode(instagramUrl)}'
          style='color:#111;margin:0 8px'
        >
          Instagram
        </a>

        <a
          href='{WebUtility.HtmlEncode(tikTokUrl)}'
          style='color:#111;margin:0 8px'
        >
          TikTok
        </a>

        <a
          href='{WebUtility.HtmlEncode(whatsAppUrl)}'
          style='color:#111;margin:0 8px'
        >
          WhatsApp
        </a>
      </div>

      <p
        style='font-size:12px;color:#999;margin-top:24px'
      >
        © nemesisbaku. Bu email avtomatik
        göndərilmişdir.
      </p>
    </div>
  </div>
</body>
</html>";
    }

    private static string GetSafeUrl(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!Uri.TryCreate(
                value.Trim(),
                UriKind.Absolute,
                out var uri))
        {
            return fallback;
        }

        if (uri.Scheme != Uri.UriSchemeHttps &&
            uri.Scheme != Uri.UriSchemeHttp)
        {
            return fallback;
        }

        return uri.ToString();
    }
}