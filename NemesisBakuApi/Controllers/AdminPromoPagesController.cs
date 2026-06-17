using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.PromoPage;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminPromoPagesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminPromoPagesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(PromoPageCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (dto.StartDate >= dto.EndDate)
            return BadRequest(ApiResponse<string>.Fail("Başlama tarixi bitmə tarixindən əvvəl olmalıdır"));

        if (dto.Type != PromoPageType.Campaign && dto.Type != PromoPageType.Banner)
            return BadRequest(ApiResponse<string>.Fail("Promo tipi düzgün deyil"));

        var usedSlots = await _context.PromoPages
            .IgnoreQueryFilters()
            .Where(x => x.Type == dto.Type && !x.IsDeleted)
            .Select(x => x.SlotNumber)
            .ToListAsync();

        var slot = Enumerable.Range(1, 5)
            .FirstOrDefault(x => !usedSlots.Contains(x));

        if (slot == 0)
            return BadRequest(ApiResponse<string>.Fail(
                dto.Type == PromoPageType.Campaign
                    ? "Maksimum 5 kampaniya yaradıla bilər"
                    : "Maksimum 5 banner yaradıla bilər"));

        var slugPrefix = dto.Type == PromoPageType.Campaign
            ? "nemesisbakucomp"
            : "nemesisbakuban";

        var promoPage = new Entities.PromoPage
        {
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            SlotNumber = slot,
            Slug = $"{slugPrefix}{slot}",
            ImageUrl = dto.ImageUrl,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive
        };

        if (dto.ProductIds.Any())
        {
            var products = await _context.Products
                .Where(x => dto.ProductIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            foreach (var productId in dto.ProductIds.Distinct())
            {
                if (!products.Contains(productId))
                    continue;

                promoPage.Products.Add(new PromoPageProduct
                {
                    ProductId = productId
                });
            }
        }

        _context.PromoPages.Add(promoPage);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(promoPage.Id, "Promo səhifə yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PromoPageType? type)
    {
        var query = _context.PromoPages
            .Include(x => x.Products)
            .AsQueryable();

        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);

        var items = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SlotNumber)
            .Select(x => new PromoPageDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                Type = x.Type,
                SlotNumber = x.SlotNumber,
                Slug = x.Slug,
                ImageUrl = x.ImageUrl,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                IsActive = x.IsActive,
                ProductIds = x.Products
                    .OrderBy(p => p.Order)
                    .Select(p => p.ProductId)
                    .ToList()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<PromoPageDto>>.Ok(items));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, PromoPageUpdateDto dto)
    {
        var promoPage = await _context.PromoPages
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (promoPage == null)
            return NotFound(ApiResponse<string>.Fail("Promo səhifə tapılmadı"));

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (dto.StartDate >= dto.EndDate)
            return BadRequest(ApiResponse<string>.Fail("Başlama tarixi bitmə tarixindən əvvəl olmalıdır"));

        promoPage.Title = dto.Title;
        promoPage.Description = dto.Description;
        promoPage.ImageUrl = dto.ImageUrl;
        promoPage.StartDate = dto.StartDate;
        promoPage.EndDate = dto.EndDate;
        promoPage.IsActive = dto.IsActive;
        promoPage.UpdatedAt = DateTime.UtcNow;

        foreach (var oldProduct in promoPage.Products)
        {
            oldProduct.IsDeleted = true;
            oldProduct.UpdatedAt = DateTime.UtcNow;
        }

        if (dto.ProductIds.Any())
        {
            var validProductIds = await _context.Products
                .Where(x => dto.ProductIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var order = 0;

            foreach (var productId in dto.ProductIds.Distinct())
            {
                if (!validProductIds.Contains(productId))
                    continue;

                promoPage.Products.Add(new PromoPageProduct
                {
                    ProductId = productId,
                    Order = order++
                });
            }
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Promo səhifə yeniləndi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var promoPage = await _context.PromoPages
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (promoPage == null)
            return NotFound(ApiResponse<string>.Fail("Promo səhifə tapılmadı"));

        promoPage.IsDeleted = true;
        promoPage.IsActive = false;
        promoPage.UpdatedAt = DateTime.UtcNow;

        foreach (var product in promoPage.Products)
        {
            product.IsDeleted = true;
            product.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Promo səhifə silindi"));
    }
}