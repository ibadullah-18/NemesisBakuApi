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

    public AdminCouriersController(AppDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException();
        return Guid.Parse(userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var couriers = await _context.CourierPhones
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new CourierPhoneDto
            {
                Id = x.Id,
                Title = x.Title,
                PhoneNumber = x.PhoneNumber,
                IsDefault = x.IsDefault
            })
            .ToListAsync();

        return Ok(ApiResponse<List<CourierPhoneDto>>.Ok(couriers));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCourierPhoneDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return BadRequest(ApiResponse<string>.Fail("Kuryer nömrəsi boş ola bilməz"));

        var phone = NormalizePhone(dto.PhoneNumber);

        var exists = await _context.CourierPhones.AnyAsync(x => x.PhoneNumber == phone);
        if (exists)
            return BadRequest(ApiResponse<string>.Fail("Bu kuryer nömrəsi artıq əlavə olunub"));

        if (dto.IsDefault)
            await ClearDefaultAsync();

        var courier = new CourierPhone
        {
            Title = dto.Title.Trim(),
            PhoneNumber = phone,
            IsDefault = dto.IsDefault
        };

        _context.CourierPhones.Add(courier);
        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Create",
            "CourierPhone",
            courier.Id.ToString(),
            $"Kuryer nömrəsi əlavə edildi: {courier.Title} - {courier.PhoneNumber}");

        return Ok(ApiResponse<Guid>.Ok(courier.Id, "Kuryer nömrəsi əlavə olundu"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, CreateCourierPhoneDto dto)
    {
        var courier = await _context.CourierPhones.FirstOrDefaultAsync(x => x.Id == id);

        if (courier == null)
            return NotFound(ApiResponse<string>.Fail("Kuryer nömrəsi tapılmadı"));

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return BadRequest(ApiResponse<string>.Fail("Kuryer nömrəsi boş ola bilməz"));

        var oldTitle = courier.Title;
        var oldPhone = courier.PhoneNumber;

        var phone = NormalizePhone(dto.PhoneNumber);

        var exists = await _context.CourierPhones.AnyAsync(x => x.Id != id && x.PhoneNumber == phone);
        if (exists)
            return BadRequest(ApiResponse<string>.Fail("Bu kuryer nömrəsi artıq əlavə olunub"));

        if (dto.IsDefault)
            await ClearDefaultAsync();

        courier.Title = dto.Title.Trim();
        courier.PhoneNumber = phone;
        courier.IsDefault = dto.IsDefault;
        courier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Update",
            "CourierPhone",
            courier.Id.ToString(),
            $"Kuryer nömrəsi yeniləndi: {oldTitle}/{oldPhone} → {courier.Title}/{courier.PhoneNumber}");

        return Ok(ApiResponse<string>.Ok("Kuryer nömrəsi yeniləndi"));
    }

    [HttpPut("{id}/default")]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        var courier = await _context.CourierPhones.FirstOrDefaultAsync(x => x.Id == id);

        if (courier == null)
            return NotFound(ApiResponse<string>.Fail("Kuryer nömrəsi tapılmadı"));

        await ClearDefaultAsync();

        courier.IsDefault = true;
        courier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "SetDefault",
            "CourierPhone",
            courier.Id.ToString(),
            $"Default kuryer nömrəsi seçildi: {courier.Title} - {courier.PhoneNumber}");

        return Ok(ApiResponse<string>.Ok("Default kuryer nömrəsi seçildi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var courier = await _context.CourierPhones.FirstOrDefaultAsync(x => x.Id == id);

        if (courier == null)
            return NotFound(ApiResponse<string>.Fail("Kuryer nömrəsi tapılmadı"));

        courier.IsDeleted = true;
        courier.IsDefault = false;
        courier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Delete",
            "CourierPhone",
            courier.Id.ToString(),
            $"Kuryer nömrəsi silindi: {courier.Title} - {courier.PhoneNumber}");

        return Ok(ApiResponse<string>.Ok("Kuryer nömrəsi silindi"));
    }

    private async Task ClearDefaultAsync()
    {
        var defaults = await _context.CourierPhones.Where(x => x.IsDefault).ToListAsync();

        foreach (var item in defaults)
        {
            item.IsDefault = false;
            item.UpdatedAt = DateTime.UtcNow;
        }
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

    private static string NormalizePhone(string phone)
    {
        return phone.Replace("+", "").Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "");
    }
}