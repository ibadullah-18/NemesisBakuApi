using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Announcement;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminEmailAnnouncementsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly EmailAnnouncementWorkerSettings _settings;

    public AdminEmailAnnouncementsController(
        AppDbContext context,
        IAuditLogService auditLogService,
        IOptions<EmailAnnouncementWorkerSettings> options)
    {
        _context = context;
        _auditLogService = auditLogService;
        _settings = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAndSend(
        CreateEmailAnnouncementDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Başlıq boş ola bilməz"));
        }

        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Açıqlama boş ola bilməz"));
        }

        var availableRecipientCount = await _context.Users
            .AsNoTracking()
            .CountAsync(
                x =>
                    !x.IsDeleted &&
                    x.IsActive &&
                    x.Email != null &&
                    x.Email != "",
                cancellationToken);

        var maxRecipients = Math.Clamp(
            _settings.MaxRecipients,
            1,
            10000);

        var totalRecipients = Math.Min(
            availableRecipientCount,
            maxRecipients);

        var announcement = new EmailAnnouncement
        {
            Title = dto.Title.Trim(),
            Description = dto.Description,

            ButtonText = string.IsNullOrWhiteSpace(dto.ButtonText)
                ? null
                : dto.ButtonText.Trim(),

            ButtonUrl = string.IsNullOrWhiteSpace(dto.ButtonUrl)
                ? null
                : dto.ButtonUrl.Trim(),

            TotalRecipients = totalRecipients,
            SentCount = 0,
            FailedCount = 0,
            CreatedByUserId = GetUserId()
        };

        _context.EmailAnnouncements.Add(announcement);

        await _context.SaveChangesAsync(cancellationToken);

        await WriteAuditLogAsync(announcement);

        var result = new
        {
            announcement.Id,
            announcement.TotalRecipients,
            announcement.SentCount,
            announcement.FailedCount
        };

        return Ok(
            ApiResponse<object>.Ok(
                result,
                totalRecipients > 0
                    ? "Elan göndərilməyə başladı"
                    : "Elan yaradıldı, göndəriləcək email tapılmadı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var historyLimit = Math.Clamp(
            _settings.HistoryLimit,
            20,
            500);

        var announcements = await _context.EmailAnnouncements
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(historyLimit)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.ButtonText,
                x.ButtonUrl,
                x.TotalRecipients,
                x.SentCount,
                x.FailedCount,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<object>.Ok(announcements));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }

    private async Task WriteAuditLogAsync(
        EmailAnnouncement announcement)
    {
        await _auditLogService.CreateAsync(
            GetUserId(),
            "Queue",
            "EmailAnnouncement",
            announcement.Id.ToString(),
            $"Email elan növbəyə əlavə edildi: " +
            $"{announcement.Title}. " +
            $"Recipients: {announcement.TotalRecipients}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}