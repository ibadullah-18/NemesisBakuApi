using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.HomeSection;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminHomeSectionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public AdminHomeSectionsController(
        AppDbContext context,
        IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new UnauthorizedAccessException();

        return parsedUserId;
    }

    private static List<Guid> NormalizeProductIds(IEnumerable<Guid>? productIds)
    {
        return (productIds ?? Enumerable.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private async Task<List<Guid>> GetValidProductIdsAsync(List<Guid> productIds)
    {
        if (productIds.Count == 0)
            return new List<Guid>();

        var validIdSet = (await _context.Products
                .AsNoTracking()
                .Where(x => productIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync())
            .ToHashSet();

        // İstifadəçinin seçdiyi sıra qorunur.
        return productIds
            .Where(validIdSet.Contains)
            .ToList();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateHomeSectionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (dto.DisplayOrder <= 0)
            return BadRequest(ApiResponse<string>.Fail("Sıra nömrəsi 0-dan böyük olmalıdır"));

        if (dto.StartDate >= dto.EndDate)
            return BadRequest(ApiResponse<string>.Fail(
                "Başlama tarixi bitmə tarixindən əvvəl olmalıdır"));

        var requestedProductIds = NormalizeProductIds(dto.ProductIds);
        var validProductIds = await GetValidProductIdsAsync(requestedProductIds);

        if (validProductIds.Count != requestedProductIds.Count)
            return BadRequest(ApiResponse<string>.Fail(
                "Seçilmiş məhsullardan biri tapılmadı və ya artıq aktiv deyil"));

        var section = new HomeSection
        {
            Title = dto.Title.Trim(),
            Subtitle = string.IsNullOrWhiteSpace(dto.Subtitle)
                ? null
                : dto.Subtitle.Trim(),
            DisplayOrder = dto.DisplayOrder,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive
        };

        for (var order = 0; order < validProductIds.Count; order++)
        {
            section.Products.Add(new HomeSectionProduct
            {
                ProductId = validProductIds[order],
                Order = order,
                IsDeleted = false
            });
        }

        _context.HomeSections.Add(section);
        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Create",
            "HomeSection",
            section.Id.ToString(),
            $"Home section yaradıldı: {section.Title}. " +
            $"DisplayOrder: {section.DisplayOrder}. " +
            $"ProductCount: {validProductIds.Count}");

        return Ok(ApiResponse<Guid>.Ok(section.Id, "Home section yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sections = await _context.HomeSections
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new HomeSectionDto
            {
                Id = x.Id,
                Title = x.Title,
                Subtitle = x.Subtitle,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                ProductIds = x.Products
                    .OrderBy(p => p.Order)
                    .Select(p => p.ProductId)
                    .ToList()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<HomeSectionDto>>.Ok(sections));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var section = await _context.HomeSections
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new HomeSectionDto
            {
                Id = x.Id,
                Title = x.Title,
                Subtitle = x.Subtitle,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                ProductIds = x.Products
                    .OrderBy(product => product.Order)
                    .Select(product => product.ProductId)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (section == null)
            return NotFound(ApiResponse<string>.Fail("Home section tapılmadı"));

        return Ok(ApiResponse<HomeSectionDto>.Ok(section));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateHomeSectionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (dto.DisplayOrder <= 0)
            return BadRequest(ApiResponse<string>.Fail("Sıra nömrəsi 0-dan böyük olmalıdır"));

        if (dto.StartDate >= dto.EndDate)
            return BadRequest(ApiResponse<string>.Fail(
                "Başlama tarixi bitmə tarixindən əvvəl olmalıdır"));

        var section = await _context.HomeSections
            .FirstOrDefaultAsync(x => x.Id == id);

        if (section == null)
            return NotFound(ApiResponse<string>.Fail("Home section tapılmadı"));

        var requestedProductIds = NormalizeProductIds(dto.ProductIds);
        var validProductIds = await GetValidProductIdsAsync(requestedProductIds);

        if (validProductIds.Count != requestedProductIds.Count)
            return BadRequest(ApiResponse<string>.Fail(
                "Seçilmiş məhsullardan biri tapılmadı və ya artıq aktiv deyil"));

        // Query filter-i keçərək əvvəllər soft-delete edilmiş əlaqələri də gətiririk.
        // Beləliklə eyni composite/unique key ilə ikinci obyekt yaratmırıq.
        var existingLinks = await _context.Set<HomeSectionProduct>()
            .IgnoreQueryFilters()
            .Where(x => x.HomeSectionId == section.Id)
            .ToListAsync();

        var existingLinkByProductId = existingLinks
            .GroupBy(x => x.ProductId)
            .ToDictionary(group => group.Key, group => group.First());

        var requestedProductIdSet = validProductIds.ToHashSet();
        var now = DateTime.UtcNow;

        var oldTitle = section.Title;
        var oldDisplayOrder = section.DisplayOrder;
        var oldActive = section.IsActive;
        var oldProductCount = existingLinks.Count(x => !x.IsDeleted);

        section.Title = dto.Title.Trim();
        section.Subtitle = string.IsNullOrWhiteSpace(dto.Subtitle)
            ? null
            : dto.Subtitle.Trim();
        section.DisplayOrder = dto.DisplayOrder;
        section.StartDate = dto.StartDate;
        section.EndDate = dto.EndDate;
        section.IsActive = dto.IsActive;
        section.UpdatedAt = now;

        // Siyahıdan çıxarılan əlaqələr soft-delete edilir.
        foreach (var existingLink in existingLinks)
        {
            if (requestedProductIdSet.Contains(existingLink.ProductId))
                continue;

            existingLink.IsDeleted = true;
            existingLink.UpdatedAt = now;
        }

        // Qalanlar yenilənir, əvvəl silinmiş əlaqələr bərpa olunur,
        // həqiqətən yeni olanlar isə yalnız bir dəfə əlavə edilir.
        for (var order = 0; order < validProductIds.Count; order++)
        {
            var productId = validProductIds[order];

            if (existingLinkByProductId.TryGetValue(productId, out var existingLink))
            {
                existingLink.Order = order;
                existingLink.IsDeleted = false;
                existingLink.UpdatedAt = now;
                continue;
            }

            _context.Set<HomeSectionProduct>().Add(new HomeSectionProduct
            {
                HomeSectionId = section.Id,
                ProductId = productId,
                Order = order,
                IsDeleted = false
            });
        }

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Update",
            "HomeSection",
            section.Id.ToString(),
            $"Home section yeniləndi: {oldTitle} → {section.Title}. " +
            $"Order: {oldDisplayOrder} → {section.DisplayOrder}. " +
            $"Active: {oldActive} → {section.IsActive}. " +
            $"ProductCount: {oldProductCount} → {validProductIds.Count}");

        return Ok(ApiResponse<string>.Ok("Home section yeniləndi"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var section = await _context.HomeSections
            .FirstOrDefaultAsync(x => x.Id == id);

        if (section == null)
            return NotFound(ApiResponse<string>.Fail("Home section tapılmadı"));

        var products = await _context.Set<HomeSectionProduct>()
            .IgnoreQueryFilters()
            .Where(x => x.HomeSectionId == section.Id)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var productCount = products.Count(x => !x.IsDeleted);

        section.IsDeleted = true;
        section.IsActive = false;
        section.UpdatedAt = now;

        foreach (var product in products)
        {
            product.IsDeleted = true;
            product.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "Delete",
            "HomeSection",
            section.Id.ToString(),
            $"Home section silindi: {section.Title}. " +
            $"ProductCount: {productCount}");

        return Ok(ApiResponse<string>.Ok("Home section silindi"));
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