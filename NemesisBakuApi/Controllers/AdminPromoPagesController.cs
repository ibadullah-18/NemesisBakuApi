using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.PromoPage;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminPromoPagesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileService _fileService;

    public AdminPromoPagesController(AppDbContext context, IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] PromoPageCreateDto dto)
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

        string? imageUrl = null;

        if (dto.File != null)
        {
            imageUrl = await _fileService.UploadImageAsync(
                dto.File,
                "promo-pages");
        }

        var promoPage = new PromoPage
        {
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            SlotNumber = slot,
            Slug = $"{slugPrefix}{slot}",
            ImageUrl = imageUrl,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive
        };

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
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(Guid id, [FromForm] PromoPageUpdateDto dto)
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
        promoPage.StartDate = dto.StartDate;
        promoPage.EndDate = dto.EndDate;
        promoPage.IsActive = dto.IsActive;
        promoPage.UpdatedAt = DateTime.UtcNow;

        if (dto.File != null)
        {
            if (!string.IsNullOrWhiteSpace(promoPage.ImageUrl))
            {
                await _fileService.DeleteImageAsync(promoPage.ImageUrl);
            }

            promoPage.ImageUrl = await _fileService.UploadImageAsync(
                dto.File,
                "promo-pages");
        }

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

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] PromoPageType? type)
    {
        var now = DateTime.UtcNow;

        var query = _context.PromoPages
            .Where(x =>
                x.IsActive &&
                x.StartDate <= now &&
                x.EndDate >= now);

        if (type.HasValue)
        {
            query = query.Where(x => x.Type == type.Value);
        }

        var items = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SlotNumber)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Type,
                x.SlotNumber,
                x.Slug,
                x.ImageUrl,
                x.StartDate,
                x.EndDate
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(items));
    }
}