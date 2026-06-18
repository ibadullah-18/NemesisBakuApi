using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Product;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromoPagesController : ControllerBase
{
    private readonly AppDbContext _context;

    public PromoPagesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var now = DateTime.Now;

        var promoPage = await _context.PromoPages
            .Include(x => x.Products)
                .ThenInclude(x => x.Product)
                    .ThenInclude(p => p.Images)
            .Include(x => x.Products)
                .ThenInclude(x => x.Product)
                    .ThenInclude(p => p.Brand)
            .Include(x => x.Products)
                .ThenInclude(x => x.Product)
                    .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(x =>
                x.Slug == slug &&
                x.IsActive &&
                x.StartDate <= now &&
                x.EndDate >= now);

        if (promoPage == null)
            return NotFound(ApiResponse<string>.Fail("Promo səhifə tapılmadı"));

        var products = promoPage.Products
            .Where(x => !x.IsDeleted && x.Product.IsActive && !x.Product.IsDeleted)
            .OrderBy(x => x.Order)
            .Select(x => new ProductListDto
            {
                Id = x.Product.Id,
                Name = x.Product.Name,
                ProductCode = x.Product.ProductCode,
                Model = x.Product.Model,
                Price = x.Product.Price,
                DiscountPrice = x.Product.DiscountPrice,
                IsDiscounted = x.Product.IsDiscounted,
                IsFeatured = x.Product.IsFeatured,
                CategoryName = x.Product.Category.Name,
                BrandName = x.Product.Brand.Name,
                MainImageUrl = x.Product.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Order)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToList();

        var result = new
        {
            promoPage.Id,
            promoPage.Title,
            promoPage.Description,
            promoPage.Type,
            promoPage.SlotNumber,
            promoPage.Slug,
            promoPage.ImageUrl,
            Products = products
        };

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] PromoPageType? type)
    {
        var now = DateTime.Now;

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
