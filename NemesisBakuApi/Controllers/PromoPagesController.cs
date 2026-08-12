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

    public PromoPagesController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(
        [FromQuery] PromoPageType? type,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        var query = _context.PromoPages
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.StartDate <= now &&
                x.EndDate >= now);

        if (type.HasValue)
        {
            query = query.Where(
                x => x.Type == type.Value);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Type,
                x.ImageUrl,
                x.MobileImageUrl,
                x.StartDate,
                x.EndDate
            })
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<object>.Ok(items));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        var promoPage = await _context.PromoPages
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x =>
                x.Id == id &&
                x.IsActive &&
                x.StartDate <= now &&
                x.EndDate >= now)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Type,
                x.ImageUrl,
                x.MobileImageUrl,

                Products = x.Products
                    .Where(item =>
                        item.Product.IsActive)
                    .OrderBy(item => item.Order)
                    .Select(item =>
                        new ProductListDto
                        {
                            Id = item.Product.Id,

                            Name =
                                item.Product.Name,

                            ProductCode =
                                item.Product
                                    .ProductCode,

                            Model =
                                item.Product.Model,

                            Price =
                                item.Product.Price,

                            DiscountPrice =
                                item.Product
                                    .DiscountPrice,

                            IsDiscounted =
                                item.Product
                                    .DiscountPrice
                                    .HasValue &&
                                item.Product
                                    .DiscountPrice
                                    .Value > 0 &&
                                item.Product
                                    .DiscountPrice
                                    .Value <
                                item.Product.Price,

                            IsFeatured =
                                item.Product
                                    .IsFeatured,

                            CategoryName =
                                item.Product
                                    .Category.Name,

                            BrandName =
                                item.Product
                                    .Brand.Name,

                            MainImageUrl =
                                item.Product.Images
                                    .OrderByDescending(
                                        image =>
                                            image.IsMain)
                                    .ThenBy(
                                        image =>
                                            image.Order)
                                    .Select(
                                        image =>
                                            image.ImageUrl)
                                    .FirstOrDefault()
                        })
                    .ToList()
            })
            .FirstOrDefaultAsync(
                cancellationToken);

        if (promoPage == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Promo səhifə tapılmadı"));
        }

        var result = new
        {
            promoPage.Id,
            promoPage.Title,
            promoPage.Description,
            promoPage.Type,
            promoPage.ImageUrl,
            promoPage.MobileImageUrl,
            promoPage.Products
        };

        return Ok(
            ApiResponse<object>.Ok(result));
    }
}