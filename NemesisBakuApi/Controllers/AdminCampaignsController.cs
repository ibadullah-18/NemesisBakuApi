using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Campaign;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using System.Security.Claims;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminCampaignsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly IFileService _fileService;

    public AdminCampaignsController(
        AppDbContext context,
        IAuditLogService auditLogService,
        IFileService fileService)
    {
        _context = context;
        _auditLogService = auditLogService;
        _fileService = fileService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CampaignCreateDto dto)
    {
        if (dto.EndDate.HasValue && dto.EndDate.Value < dto.StartDate)
            return BadRequest(ApiResponse<string>.Fail("Bitmə tarixi başlama tarixindən əvvəl ola bilməz"));

        string? imageUrl = null;

        if (dto.File != null && dto.File.Length > 0)
        {
            imageUrl = await _fileService.UploadImageAsync(dto.File, "campaigns");
        }

        var campaign = new Campaign
        {
            Title = dto.Title,
            Description = dto.Description,
            ImageUrl = imageUrl,
            RedirectUrl = dto.RedirectUrl,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Create",
            "Campaign",
            campaign.Id.ToString(),
            $"Kampaniya yaradıldı: {campaign.Title}");

        return Ok(ApiResponse<Guid>.Ok(campaign.Id, "Kampaniya yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var campaigns = await _context.Campaigns
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CampaignDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                ImageUrl = x.ImageUrl,
                RedirectUrl = x.RedirectUrl,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return Ok(ApiResponse<List<CampaignDto>>.Ok(campaigns));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] CampaignCreateDto dto)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(x => x.Id == id);

        if (campaign == null)
            return NotFound(ApiResponse<string>.Fail("Kampaniya tapılmadı"));

        if (dto.EndDate.HasValue && dto.EndDate.Value < dto.StartDate)
            return BadRequest(ApiResponse<string>.Fail("Bitmə tarixi başlama tarixindən əvvəl ola bilməz"));

        if (dto.File != null && dto.File.Length > 0)
        {
            if (!string.IsNullOrWhiteSpace(campaign.ImageUrl))
            {
                await _fileService.DeleteImageAsync(campaign.ImageUrl);
            }

            campaign.ImageUrl = await _fileService.UploadImageAsync(dto.File, "campaigns");
        }

        campaign.Title = dto.Title;
        campaign.Description = dto.Description;
        campaign.RedirectUrl = dto.RedirectUrl;
        campaign.StartDate = dto.StartDate;
        campaign.EndDate = dto.EndDate;
        campaign.IsActive = dto.IsActive;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Update",
            "Campaign",
            campaign.Id.ToString(),
            $"Kampaniya yeniləndi: {campaign.Title}");

        return Ok(ApiResponse<string>.Ok("Kampaniya yeniləndi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(x => x.Id == id);

        if (campaign == null)
            return NotFound(ApiResponse<string>.Fail("Kampaniya tapılmadı"));

        if (!string.IsNullOrWhiteSpace(campaign.ImageUrl))
        {
            await _fileService.DeleteImageAsync(campaign.ImageUrl);
        }

        campaign.IsDeleted = true;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Delete",
            "Campaign",
            campaign.Id.ToString(),
            $"Kampaniya silindi: {campaign.Title}");

        return Ok(ApiResponse<string>.Ok("Kampaniya silindi"));
    }

    private Guid? GetUserIdOrNull()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return Guid.Parse(userId);
    }

    private async Task WriteAuditLogAsync(
        string action,
        string entityName,
        string? entityId,
        string? description)
    {
        await _auditLogService.CreateAsync(
            GetUserIdOrNull(),
            action,
            entityName,
            entityId,
            description,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}