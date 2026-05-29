using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.PromoCode;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminPromoCodesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminPromoCodesController(AppDbContext context)
    {
        _context = context;
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

        return Ok(ApiResponse<string>.Ok("Promo kod silindi"));
    }
}