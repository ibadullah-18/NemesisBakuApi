using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.PromoCode;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

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
    public async Task<IActionResult> Create(
        PromoCodeCreateDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Promo kod boş ola bilməz"));
        }

        var code = dto.Code
            .Trim()
            .ToUpperInvariant();

        var exists = await _context.PromoCodes
            .AsNoTracking()
            .AnyAsync(
                x => x.Code == code,
                cancellationToken);

        if (exists)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Promo kod artıq mövcuddur"));
        }

        if (dto.DiscountValue <= 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Endirim dəyəri düzgün deyil"));
        }

        if (dto.UsageLimit.HasValue &&
            dto.UsageLimit.Value <= 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "İstifadə limiti düzgün deyil"));
        }

        if (dto.MinOrderAmount.HasValue &&
            dto.MinOrderAmount.Value < 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Minimum sifariş məbləği düzgün deyil"));
        }

        if (dto.EndDate.HasValue &&
            dto.EndDate.Value < dto.StartDate)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bitmə tarixi başlanğıc tarixindən " +
                    "əvvəl ola bilməz"));
        }

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

        await _context.SaveChangesAsync(
            cancellationToken);

        await WriteAuditLogAsync(
            "Create",
            promo,
            $"Promo kod yaradıldı: {promo.Code}");

        return Ok(
            ApiResponse<Guid>.Ok(
                promo.Id,
                "Promo kod yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var promos = await _context.PromoCodes
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<object>.Ok(promos));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var promo = await _context.PromoCodes
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (promo == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Promo kod tapılmadı"));
        }

        promo.IsDeleted = true;
        promo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await WriteAuditLogAsync(
            "Delete",
            promo,
            $"Promo kod silindi: {promo.Code}");

        return Ok(
            ApiResponse<string>.Ok(
                "Promo kod silindi"));
    }

    private Guid? GetUserIdOrNull()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    private async Task WriteAuditLogAsync(
        string action,
        PromoCode promo,
        string description)
    {
        await _auditLogService.CreateAsync(
            GetUserIdOrNull(),
            action,
            "PromoCode",
            promo.Id.ToString(),
            description,
            HttpContext.Connection
                .RemoteIpAddress?
                .ToString(),
            Request.Headers.UserAgent.ToString());
    }
}