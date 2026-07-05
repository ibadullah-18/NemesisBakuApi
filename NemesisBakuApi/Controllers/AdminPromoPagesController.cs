using System.Security.Claims;
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
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminPromoPagesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileService _fileService;
    private readonly IAuditLogService _auditLogService;

    public AdminPromoPagesController(
        AppDbContext context,
        IFileService fileService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _fileService = fileService;
        _auditLogService = auditLogService;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException();

        return Guid.Parse(userId);
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

        if (dto.IsActive)
        {
            var activeCount = await _context.PromoPages
                .CountAsync(x => x.Type == dto.Type && x.IsActive);

            if (activeCount >= 5)
            {
                return BadRequest(ApiResponse<string>.Fail(
                    dto.Type == PromoPageType.Campaign
                        ? "Eyni anda maksimum 5 aktiv kampaniya ola bilər"
                        : "Eyni anda maksimum 5 aktiv banner ola bilər"));
            }
        }

        string? imageUrl = null;

        if (dto.File != null)
            imageUrl = await _fileService.UploadImageAsync(dto.File, "promo-pages");

        var promoPage = new PromoPage
        {
            Title = dto.Title.Trim(),
            Description = dto.Description,
            Type = dto.Type,
            ImageUrl = imageUrl,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive
        };

        var productIds = dto.ProductIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (productIds.Any())
        {
            var validProductIds = await _context.Products
                .Where(x => productIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var order = 0;

            foreach (var productId in productIds)
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

        await WriteAuditLogAsync(
            "Create",
            "PromoPage",
            promoPage.Id.ToString(),
            $"Promo yaradıldı: {promoPage.Title}, Type: {promoPage.Type}, ProductCount: {productIds.Count}");

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
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PromoPageDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                Type = x.Type,
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

        if (!promoPage.IsActive && dto.IsActive)
        {
            var activeCount = await _context.PromoPages
                .CountAsync(x => x.Type == promoPage.Type && x.IsActive && x.Id != promoPage.Id);

            if (activeCount >= 5)
            {
                return BadRequest(ApiResponse<string>.Fail(
                    promoPage.Type == PromoPageType.Campaign
                        ? "Eyni anda maksimum 5 aktiv kampaniya ola bilər"
                        : "Eyni anda maksimum 5 aktiv banner ola bilər"));
            }
        }

        var oldTitle = promoPage.Title;
        var oldActive = promoPage.IsActive;

        promoPage.Title = dto.Title.Trim();
        promoPage.Description = dto.Description;
        promoPage.StartDate = dto.StartDate;
        promoPage.EndDate = dto.EndDate;
        promoPage.IsActive = dto.IsActive;
        promoPage.UpdatedAt = DateTime.UtcNow;

        var imageChanged = false;

        if (dto.File != null)
        {
            if (!string.IsNullOrWhiteSpace(promoPage.ImageUrl))
                await _fileService.DeleteImageAsync(promoPage.ImageUrl);

            promoPage.ImageUrl = await _fileService.UploadImageAsync(dto.File, "promo-pages");
            imageChanged = true;
        }

        var oldProducts = await _context.PromoPageProducts
            .IgnoreQueryFilters()
            .Where(x => x.PromoPageId == promoPage.Id)
            .ToListAsync();

        _context.PromoPageProducts.RemoveRange(oldProducts);

        var productIds = dto.ProductIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (productIds.Any())
        {
            var validProductIds = await _context.Products
                .Where(x => productIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var order = 0;

            foreach (var productId in productIds)
            {
                if (!validProductIds.Contains(productId))
                    continue;

                _context.PromoPageProducts.Add(new PromoPageProduct
                {
                    PromoPageId = promoPage.Id,
                    ProductId = productId,
                    Order = order++
                });
            }
        }

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Update",
            "PromoPage",
            promoPage.Id.ToString(),
            $"Promo yeniləndi: {oldTitle} → {promoPage.Title}. Active: {oldActive} → {promoPage.IsActive}. ProductCount: {productIds.Count}. ImageChanged: {imageChanged}");

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

        await WriteAuditLogAsync(
            "Delete",
            "PromoPage",
            promoPage.Id.ToString(),
            $"Promo silindi: {promoPage.Title}, Type: {promoPage.Type}");

        return Ok(ApiResponse<string>.Ok("Promo səhifə silindi"));
    }

    private async Task WriteAuditLogAsync(
        string action,
        string entityName,
        string? entityId,
        string? description)
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