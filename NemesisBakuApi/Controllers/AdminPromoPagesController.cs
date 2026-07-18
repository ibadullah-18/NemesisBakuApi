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
    private const int MaxActivePromosPerType = 5;

    // Mövcud cədvəldə EndDate sütunu saxlanılır, amma artıq admin formunda
    // idarə olunmur. Bu tarix promo üçün praktik olaraq "müddətsiz" deməkdir.
    private static readonly DateTime NoExpiryDate =
        new(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);

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

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new UnauthorizedAccessException();

        return parsedUserId;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] PromoPageCreateDto dto)
    {
        if (!Enum.IsDefined(typeof(PromoPageType), dto.Type) ||
            (dto.Type != PromoPageType.Campaign && dto.Type != PromoPageType.Banner))
        {
            return BadRequest(ApiResponse<string>.Fail("Promo tipi düzgün deyil"));
        }

        if (dto.File == null || dto.File.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("Promo şəkli seçilməlidir"));

        if (dto.StartDate == default)
            return BadRequest(ApiResponse<string>.Fail("Başlama tarixi seçilməlidir"));

        var productIds = NormalizeProductIds(dto.ProductIds);
        var validProductIds = await GetValidProductIdsAsync(productIds);

        if (validProductIds.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("Ən azı bir etibarlı məhsul seçilməlidir"));

        if (validProductIds.Count != productIds.Count)
            return BadRequest(ApiResponse<string>.Fail("Seçilmiş məhsullardan biri tapılmadı"));

        if (dto.IsActive && await HasReachedActiveLimitAsync(dto.Type))
            return ActiveLimitError(dto.Type);

        string? uploadedImageUrl = null;
        PromoPage promoPage;

        try
        {
            uploadedImageUrl = await _fileService.UploadImageAsync(dto.File!, "promo-pages");

            promoPage = new PromoPage
            {
                // Köhnə DB sxemində Title NOT NULL ola bilər. Dəyər yalnız daxili
                // uyğunluq üçündür və heç bir DTO-da istifadəçiyə qaytarılmır.
                Title = $"promo-{Guid.NewGuid():N}",
                Description = string.Empty,
                Type = dto.Type,
                ImageUrl = uploadedImageUrl,
                StartDate = ToUtc(dto.StartDate),
                EndDate = NoExpiryDate,
                IsActive = dto.IsActive
            };

            var order = 0;

            foreach (var productId in productIds)
            {
                promoPage.Products.Add(new PromoPageProduct
                {
                    ProductId = productId,
                    Order = order++
                });
            }

            _context.PromoPages.Add(promoPage);
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(uploadedImageUrl))
                await TryDeleteImageAsync(uploadedImageUrl);

            throw;
        }

        await WriteAuditLogAsync(
            "Create",
            "PromoPage",
            promoPage.Id.ToString(),
            $"Promo yaradıldı. Type: {promoPage.Type}, ProductCount: {productIds.Count}");

        return Ok(ApiResponse<Guid>.Ok(promoPage.Id, "Promo yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PromoPageType? type)
    {
        var query = _context.PromoPages.AsNoTracking().AsQueryable();

        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);

        var items = await ProjectPromo(query)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync();

        return Ok(ApiResponse<List<PromoPageDto>>.Ok(items));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var promo = await ProjectPromo(
                _context.PromoPages.AsNoTracking().Where(x => x.Id == id))
            .FirstOrDefaultAsync();

        if (promo == null)
            return NotFound(ApiResponse<string>.Fail("Promo tapılmadı"));

        return Ok(ApiResponse<PromoPageDto>.Ok(promo));
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(Guid id, [FromForm] PromoPageUpdateDto dto)
    {
        var promoPage = await _context.PromoPages
            .FirstOrDefaultAsync(x => x.Id == id);

        if (promoPage == null)
            return NotFound(ApiResponse<string>.Fail("Promo tapılmadı"));

        if (dto.StartDate == default)
            return BadRequest(ApiResponse<string>.Fail("Başlama tarixi seçilməlidir"));

        var productIds = NormalizeProductIds(dto.ProductIds);
        var validProductIds = await GetValidProductIdsAsync(productIds);

        if (validProductIds.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("Ən azı bir etibarlı məhsul seçilməlidir"));

        if (validProductIds.Count != productIds.Count)
            return BadRequest(ApiResponse<string>.Fail("Seçilmiş məhsullardan biri tapılmadı"));

        if (!promoPage.IsActive && dto.IsActive &&
            await HasReachedActiveLimitAsync(promoPage.Type, promoPage.Id))
        {
            return ActiveLimitError(promoPage.Type);
        }

        string? newImageUrl = null;
        var oldImageUrl = promoPage.ImageUrl;

        try
        {
            // Yeni şəkil əvvəl yüklənir. Upload uğursuz olsa köhnə şəkil itmir.
            if (dto.File != null && dto.File.Length > 0)
                newImageUrl = await _fileService.UploadImageAsync(dto.File, "promo-pages");

            promoPage.ImageUrl = newImageUrl ?? oldImageUrl;
            promoPage.StartDate = ToUtc(dto.StartDate);
            promoPage.EndDate = NoExpiryDate;
            promoPage.IsActive = dto.IsActive;
            promoPage.UpdatedAt = DateTime.UtcNow;

            var oldProducts = await _context.PromoPageProducts
                .IgnoreQueryFilters()
                .Where(x => x.PromoPageId == promoPage.Id)
                .ToListAsync();

            _context.PromoPageProducts.RemoveRange(oldProducts);

            var order = 0;

            foreach (var productId in productIds)
            {
                _context.PromoPageProducts.Add(new PromoPageProduct
                {
                    PromoPageId = promoPage.Id,
                    ProductId = productId,
                    Order = order++
                });
            }

            await _context.SaveChangesAsync();
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(newImageUrl))
                await TryDeleteImageAsync(newImageUrl);

            throw;
        }

        if (!string.IsNullOrWhiteSpace(newImageUrl) &&
            !string.IsNullOrWhiteSpace(oldImageUrl))
        {
            await TryDeleteImageAsync(oldImageUrl);
        }

        await WriteAuditLogAsync(
            "Update",
            "PromoPage",
            promoPage.Id.ToString(),
            $"Promo yeniləndi. Active: {promoPage.IsActive}. ProductCount: {productIds.Count}. ImageChanged: {newImageUrl != null}");

        return Ok(ApiResponse<string>.Ok("Promo yeniləndi"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var promoPage = await _context.PromoPages
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (promoPage == null)
            return NotFound(ApiResponse<string>.Fail("Promo tapılmadı"));

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
            $"Promo silindi. Type: {promoPage.Type}");

        return Ok(ApiResponse<string>.Ok("Promo silindi"));
    }

    private static IQueryable<PromoPageDto> ProjectPromo(IQueryable<PromoPage> query)
    {
        return query.Select(x => new PromoPageDto
        {
            Id = x.Id,
            Type = x.Type,
            ImageUrl = x.ImageUrl,
            StartDate = x.StartDate,
            IsActive = x.IsActive,
            ProductIds = x.Products
                .OrderBy(p => p.Order)
                .Select(p => p.ProductId)
                .ToList()
        });
    }

    private static List<Guid> NormalizeProductIds(IEnumerable<Guid>? productIds)
    {
        return (productIds ?? Enumerable.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private async Task<HashSet<Guid>> GetValidProductIdsAsync(List<Guid> productIds)
    {
        var ids = await _context.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        return ids.ToHashSet();
    }

    private async Task<bool> HasReachedActiveLimitAsync(
        PromoPageType type,
        Guid? excludedId = null)
    {
        var now = DateTime.UtcNow;
        var query = _context.PromoPages
            .AsNoTracking()
            .Where(x => x.Type == type && x.IsActive && x.EndDate >= now);

        if (excludedId.HasValue)
            query = query.Where(x => x.Id != excludedId.Value);

        return await query.CountAsync() >= MaxActivePromosPerType;
    }

    private IActionResult ActiveLimitError(PromoPageType type)
    {
        var message = type == PromoPageType.Campaign
            ? "Eyni anda maksimum 5 aktiv kampaniya ola bilər"
            : "Eyni anda maksimum 5 aktiv banner ola bilər";

        return BadRequest(ApiResponse<string>.Fail(message));
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private async Task TryDeleteImageAsync(string imageUrl)
    {
        try
        {
            await _fileService.DeleteImageAsync(imageUrl);
        }
        catch
        {
            // DB əməliyyatının uğurunu CDN təmizləmə xətasına görə geri çevirmirik.
        }
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
