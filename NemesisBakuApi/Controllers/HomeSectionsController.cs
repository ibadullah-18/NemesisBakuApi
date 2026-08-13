using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.HomeSection;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeSectionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public HomeSectionsController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("active")]
    [OutputCache(
        PolicyName = ProductCacheTags.ProductListsPolicy)]
    public async Task<IActionResult> GetActive(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var sections = await _context.HomeSections
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x =>
                x.IsActive &&
                x.StartDate <= now &&
                x.EndDate >= now)
            .OrderBy(x => x.DisplayOrder)
            .Take(20)
            .Select(x => new ActiveHomeSectionDto
            {
                Id = x.Id,
                Title = x.Title,
                Subtitle = x.Subtitle,
                DisplayOrder = x.DisplayOrder,

                Products = x.Products
                    .Where(product =>
                        product.Product.IsActive)
                    .OrderBy(product =>
                        product.Order)
                    .Take(50)
                    .Select(product =>
                        new HomeSectionProductDto
                        {
                            Id = product.Product.Id,

                            Name =
                                product.Product.Name,

                            ProductCode =
                                product.Product
                                    .ProductCode,

                            Price =
                                product.Product.Price,

                            DiscountPrice =
                                product.Product
                                    .DiscountPrice,

                            IsDiscounted =
                                product.Product
                                    .DiscountPrice
                                    .HasValue &&
                                product.Product
                                    .DiscountPrice
                                    .Value > 0 &&
                                product.Product
                                    .DiscountPrice
                                    .Value <
                                product.Product.Price,

                            ImageUrl =
                                product.Product.Images
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
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<List<ActiveHomeSectionDto>>
                .Ok(sections));
    }
}