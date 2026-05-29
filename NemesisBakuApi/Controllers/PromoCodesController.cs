using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.PromoCode;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromoCodesController : ControllerBase
{
    private readonly AppDbContext _context;

    public PromoCodesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check(CheckPromoCodeDto dto)
    {
        var now = DateTime.UtcNow;
        var code = dto.Code.Trim().ToUpper();

        var promo = await _context.PromoCodes
            .FirstOrDefaultAsync(x =>
                x.Code == code &&
                x.IsActive &&
                x.StartDate <= now &&
                (!x.EndDate.HasValue || x.EndDate.Value >= now));

        if (promo == null)
        {
            return BadRequest(ApiResponse<PromoCodeCheckResultDto>.Fail("Promo kod yanlışdır və ya aktiv deyil"));
        }

        if (promo.UsageLimit.HasValue && promo.UsedCount >= promo.UsageLimit.Value)
        {
            return BadRequest(ApiResponse<PromoCodeCheckResultDto>.Fail("Promo kod istifadə limitinə çatıb"));
        }

        if (promo.MinOrderAmount.HasValue && dto.OrderAmount < promo.MinOrderAmount.Value)
        {
            return BadRequest(ApiResponse<PromoCodeCheckResultDto>.Fail(
                $"Bu promo kod üçün minimum sifariş məbləği {promo.MinOrderAmount.Value} AZN olmalıdır"));
        }

        decimal discountAmount = promo.DiscountType == DiscountType.Percentage
            ? dto.OrderAmount * promo.DiscountValue / 100
            : promo.DiscountValue;

        if (discountAmount > dto.OrderAmount)
            discountAmount = dto.OrderAmount;

        var result = new PromoCodeCheckResultDto
        {
            IsValid = true,
            DiscountAmount = discountAmount,
            Message = "Promo kod tətbiq oluna bilər"
        };

        return Ok(ApiResponse<PromoCodeCheckResultDto>.Ok(result));
    }
}