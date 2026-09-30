using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Product;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController, Route("api/[controller]")]
public class ShowcaseController(AppDbContext db) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct) => Ok(ApiResponse<object>.Ok(
        await db.ShowcaseGroups.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Blocks = x.Blocks.OrderBy(b => b.DisplayOrder).Select(b => new
                {
                    b.Id, b.Shape, b.TargetType, b.Slug, b.ExternalUrl,
                    b.ImageUrl, b.MobileImageUrl, b.ImageAlt
                }).ToList()
            }).ToListAsync(ct)));

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> Page(string slug, CancellationToken ct)
    {
        var normalized = ShowcaseValidation.NormalizeSlug(slug);
        if (normalized == null) return NotFound(ApiResponse<string>.Fail("Səhifə tapılmadı."));
        var block = await db.ShowcaseBlocks.AsNoTracking().AsSplitQuery()
            .Where(x => x.Slug == normalized && x.TargetType == "internal" && x.Group.IsActive)
            .Select(x => new
            {
                x.Id, x.Shape, x.ImageUrl, x.MobileImageUrl, x.ImageAlt,
                x.Title, x.Subtitle, x.Description, x.AfterProductsDescription,
                Products = x.Products.Where(p => p.Product.IsActive).OrderBy(p => p.DisplayOrder)
                    .Select(p => new ProductListDto
                    {
                        Id = p.Product.Id, Name = p.Product.Name, ProductCode = p.Product.ProductCode,
                        Model = p.Product.Model, Price = p.Product.Price, DiscountPrice = p.Product.DiscountPrice,
                        IsDiscounted = p.Product.DiscountPrice.HasValue && p.Product.DiscountPrice > 0 && p.Product.DiscountPrice < p.Product.Price,
                        IsFeatured = p.Product.IsFeatured, CategoryName = p.Product.Category.Name, BrandName = p.Product.Brand.Name,
                        MainImageUrl = p.Product.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.Order).Select(i => i.ImageUrl).FirstOrDefault(),
                        TotalStock = p.Product.Variants.Where(v => v.IsActive).Sum(v => v.StockCount)
                    }).ToList()
            }).FirstOrDefaultAsync(ct);
        return block == null ? NotFound(ApiResponse<string>.Fail("Səhifə tapılmadı.")) : Ok(ApiResponse<object>.Ok(block));
    }
}
