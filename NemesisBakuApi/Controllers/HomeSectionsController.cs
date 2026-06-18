using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.HomeSection;
using NemesisBakuApi.Helpers;

[ApiController]
[Route("api/[controller]")]
public class HomeSectionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public HomeSectionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var now = DateTime.UtcNow;

        var sections = await _context.HomeSections

            .Include(x => x.Products)
                .ThenInclude(x => x.Product)
                    .ThenInclude(x => x.Images)

            .Where(x =>
                x.IsActive &&
                x.StartDate <= now &&
                x.EndDate >= now)

            .OrderBy(x => x.DisplayOrder)

            .Select(x => new ActiveHomeSectionDto
            {
                Id = x.Id,

                Title = x.Title,

                Subtitle = x.Subtitle,

                DisplayOrder = x.DisplayOrder,

                Products = x.Products

                    .OrderBy(p => p.Order)

                    .Select(p => new HomeSectionProductDto
                    {
                        Id = p.Product.Id,

                        Name = p.Product.Name,

                        ProductCode = p.Product.ProductCode,

                        Price = p.Product.Price,

                        DiscountPrice = p.Product.DiscountPrice,

                        IsDiscounted =
                            p.Product.DiscountPrice.HasValue &&
                            p.Product.DiscountPrice.Value > 0 &&
                            p.Product.DiscountPrice.Value < p.Product.Price,

                        ImageUrl = p.Product.Images

                            .OrderByDescending(i => i.IsMain)

                            .ThenBy(i => i.Order)

                            .Select(i => i.ImageUrl)

                            .FirstOrDefault()

                    }).ToList()

            }).ToListAsync();

        return Ok(ApiResponse<List<ActiveHomeSectionDto>>.Ok(sections));
    }
}