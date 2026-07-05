using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.PromoCode;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using System.Security.Claims;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminPromoCodesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminPromoCodesController(
        AppDbContext context,
        IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(PromoCodeCreateDto dto)
    {
        var code = dto.Code.Trim().ToUpper();

        if (await _context.PromoCodes.AnyAsync(x => x.Code == code))
            return BadRequest(ApiResponse<string>.Fail("Promo kod artıq mövcuddur"));

        if (dto.DiscountValue <= 0)
            return BadRequest(ApiResponse<string>.Fail("Endirim dəyəri düzgün deyil"));

        var promo = new PromoCode
        {
            Code = code,
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            UsageLimit = dto.UsageLimit,
            MinOrderAmount = dto.MinOrderAmount,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive
        };

        _context.PromoCodes.Add(promo);
        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Create",
            "PromoCode",
            promo.Id.ToString(),
            $"Promo kod yaradıldı: {promo.Code}");

        return Ok(ApiResponse<Guid>.Ok(promo.Id, "Promo kod yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var promos = await _context.PromoCodes
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(promos));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var promo = await _context.PromoCodes.FirstOrDefaultAsync(x => x.Id == id);

        if (promo == null)
            return NotFound(ApiResponse<string>.Fail("Promo kod tapılmadı"));

        promo.IsDeleted = true;
        promo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Delete",
            "PromoCode",
            promo.Id.ToString(),
            $"Promo kod silindi: {promo.Code}");

        return Ok(ApiResponse<string>.Ok("Promo kod silindi"));
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