using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Courier;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminCouriersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminCouriersController(
        AppDbContext context,
        IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
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

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var couriers = await _context.CourierPhones
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new CourierPhoneDto
            {
                Id = x.Id,
                Title = x.Title,
                PhoneNumber = x.PhoneNumber,
                IsDefault = x.IsDefault
            })
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<List<CourierPhoneDto>>
                .Ok(couriers));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCourierPhoneDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Başlıq boş ola bilməz"));
        }

        if (string.IsNullOrWhiteSpace(
                dto.PhoneNumber))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Kuryer nömrəsi boş ola bilməz"));
        }

        var phone = NormalizePhone(
            dto.PhoneNumber);

        var exists = await _context.CourierPhones
            .AsNoTracking()
            .AnyAsync(
                x => x.PhoneNumber == phone,
                cancellationToken);

        if (exists)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu kuryer nömrəsi artıq əlavə olunub"));
        }

        if (dto.IsDefault)
        {
            await ClearDefaultAsync(
                cancellationToken);
        }

        var courier = new CourierPhone
        {
            Title = dto.Title.Trim(),
            PhoneNumber = phone,
            IsDefault = dto.IsDefault
        };

        _context.CourierPhones.Add(courier);

        await _context.SaveChangesAsync(
            cancellationToken);

        await WriteAuditLogAsync(
            "Create",
            courier,
            $"Kuryer nömrəsi əlavə edildi: " +
            $"{courier.Title} - {courier.PhoneNumber}");

        return Ok(
            ApiResponse<Guid>.Ok(
                courier.Id,
                "Kuryer nömrəsi əlavə olundu"));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        CreateCourierPhoneDto dto,
        CancellationToken cancellationToken)
    {
        var courier = await _context.CourierPhones
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (courier == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Kuryer nömrəsi tapılmadı"));
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Başlıq boş ola bilməz"));
        }

        if (string.IsNullOrWhiteSpace(
                dto.PhoneNumber))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Kuryer nömrəsi boş ola bilməz"));
        }

        var phone = NormalizePhone(
            dto.PhoneNumber);

        var exists = await _context.CourierPhones
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id != id &&
                    x.PhoneNumber == phone,
                cancellationToken);

        if (exists)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu kuryer nömrəsi artıq əlavə olunub"));
        }

        var oldTitle = courier.Title;
        var oldPhone = courier.PhoneNumber;

        if (dto.IsDefault)
        {
            await ClearDefaultAsync(
                cancellationToken);
        }

        courier.Title = dto.Title.Trim();
        courier.PhoneNumber = phone;
        courier.IsDefault = dto.IsDefault;
        courier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await WriteAuditLogAsync(
            "Update",
            courier,
            $"Kuryer nömrəsi yeniləndi: " +
            $"{oldTitle}/{oldPhone} → " +
            $"{courier.Title}/{courier.PhoneNumber}");

        return Ok(
            ApiResponse<string>.Ok(
                "Kuryer nömrəsi yeniləndi"));
    }

    [HttpPut("{id:guid}/default")]
    public async Task<IActionResult> SetDefault(
        Guid id,
        CancellationToken cancellationToken)
    {
        var courier = await _context.CourierPhones
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (courier == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Kuryer nömrəsi tapılmadı"));
        }

        await ClearDefaultAsync(
            cancellationToken);

        courier.IsDefault = true;
        courier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await WriteAuditLogAsync(
            "SetDefault",
            courier,
            $"Default kuryer nömrəsi seçildi: " +
            $"{courier.Title} - {courier.PhoneNumber}");

        return Ok(
            ApiResponse<string>.Ok(
                "Default kuryer nömrəsi seçildi"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var courier = await _context.CourierPhones
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (courier == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Kuryer nömrəsi tapılmadı"));
        }

        courier.IsDeleted = true;
        courier.IsDefault = false;
        courier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await WriteAuditLogAsync(
            "Delete",
            courier,
            $"Kuryer nömrəsi silindi: " +
            $"{courier.Title} - {courier.PhoneNumber}");

        return Ok(
            ApiResponse<string>.Ok(
                "Kuryer nömrəsi silindi"));
    }

    private async Task ClearDefaultAsync(
        CancellationToken cancellationToken)
    {
        var updatedAt = DateTime.UtcNow;

        await _context.CourierPhones
            .Where(x => x.IsDefault)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.IsDefault,
                        false)
                    .SetProperty(
                        x => x.UpdatedAt,
                        updatedAt),
                cancellationToken);
    }

    private async Task WriteAuditLogAsync(
        string action,
        CourierPhone courier,
        string description)
    {
        await _auditLogService.CreateAsync(
            GetUserId(),
            action,
            "CourierPhone",
            courier.Id.ToString(),
            description,
            HttpContext.Connection
                .RemoteIpAddress?
                .ToString(),
            Request.Headers.UserAgent.ToString());
    }

    private static string NormalizePhone(
        string phone)
    {
        return phone
            .Replace("+", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("-", "");
    }
}