using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Announcement;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminEmailAnnouncementsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;

    public AdminEmailAnnouncementsController(
        AppDbContext context,
        IEmailService emailService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _emailService = emailService;
        _auditLogService = auditLogService;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException();
        return Guid.Parse(userId);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAndSend(CreateEmailAnnouncementDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest(ApiResponse<string>.Fail("Açıqlama boş ola bilməz"));

        var users = await _context.Users
            .Where(x => !x.IsDeleted && x.IsActive && !string.IsNullOrWhiteSpace(x.Email))
            .ToListAsync();

        var announcement = new EmailAnnouncement
        {
            Title = dto.Title.Trim(),
            Description = dto.Description,
            ButtonText = dto.ButtonText,
            ButtonUrl = dto.ButtonUrl,
            TotalRecipients = users.Count,
            CreatedByUserId = GetUserId()
        };

        _context.EmailAnnouncements.Add(announcement);

        foreach (var user in users)
        {
            var sent = await _emailService.SendAnnouncementAsync(
                user.Email!,
                announcement.Title,
                announcement.Description,
                announcement.ButtonText,
                announcement.ButtonUrl);

            if (sent) announcement.SentCount++;
            else announcement.FailedCount++;
        }

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Send",
            "EmailAnnouncement",
            announcement.Id.ToString(),
            $"Email elan göndərildi: {announcement.Title}. Recipients: {announcement.TotalRecipients}, Sent: {announcement.SentCount}, Failed: {announcement.FailedCount}");

        return Ok(ApiResponse<object>.Ok(new
        {
            announcement.Id,
            announcement.TotalRecipients,
            announcement.SentCount,
            announcement.FailedCount
        }, "Elan göndərildi"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var announcements = await _context.EmailAnnouncements
            .OrderByDescending(x => x.CreatedAt)
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
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(announcements));
    }

    private async Task WriteAuditLogAsync(string action, string entityName, string? entityId, string? description)
    {
        await _auditLogService.CreateAsync(
            GetUserId(),
            action,
            entityName,
            entityId,
            description,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}