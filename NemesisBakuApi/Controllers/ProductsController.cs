using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Product;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductFilterDto filter)
    {
        if (filter.Page <= 0)
            filter.Page = 1;

        if (filter.PageSize <= 0)
            filter.PageSize = 20;

        if (filter.PageSize > 100)
            filter.PageSize = 100;

        var query = _context.Products
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Images)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Size)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Color)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == filter.CategoryId.Value);
        }

        if (filter.BrandId.HasValue)
        {
            query = query.Where(x => x.BrandId == filter.BrandId.Value);
        }

        if (filter.SizeId.HasValue)
        {
            query = query.Where(x =>
                x.Variants.Any(v =>
                    v.SizeId == filter.SizeId.Value &&
                    v.StockCount > 0 &&
                    v.IsActive));
        }

        if (filter.ColorId.HasValue)
        {
            query = query.Where(x =>
                x.Variants.Any(v =>
                    v.ColorId == filter.ColorId.Value &&
                    v.StockCount > 0 &&
                    v.IsActive));
        }

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            var model = filter.Model.Trim().ToLower();

            query = query.Where(x =>
                x.Model != null &&
                x.Model.ToLower().Contains(model));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();

            query = query.Where(x =>
                x.Name.ToLower().Contains(search) ||
                x.ProductCode.ToLower().Contains(search) ||
                (x.Model != null && x.Model.ToLower().Contains(search)) ||
                (x.Description != null && x.Description.ToLower().Contains(search)) ||
                x.Brand.Name.ToLower().Contains(search) ||
                x.Category.Name.ToLower().Contains(search));
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(x =>
                (x.IsDiscounted && x.DiscountPrice.HasValue
                    ? x.DiscountPrice.Value
                    : x.Price) >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(x =>
                (x.IsDiscounted && x.DiscountPrice.HasValue
                    ? x.DiscountPrice.Value
                    : x.Price) <= filter.MaxPrice.Value);
        }

        if (filter.IsDiscounted.HasValue)
        {
            query = query.Where(x => x.IsDiscounted == filter.IsDiscounted.Value);
        }

        if (filter.IsFeatured.HasValue)
        {
            query = query.Where(x => x.IsFeatured == filter.IsFeatured.Value);
        }

        query = filter.Sort?.ToLower() switch
        {
            "price_asc" => query.OrderBy(x =>
                x.IsDiscounted && x.DiscountPrice.HasValue
                    ? x.DiscountPrice.Value
                    : x.Price),

            "price_desc" => query.OrderByDescending(x =>
                x.IsDiscounted && x.DiscountPrice.HasValue
                    ? x.DiscountPrice.Value
                    : x.Price),

            "popular" => query.OrderByDescending(x => x.ViewCount),

            "discounted" => query
                .OrderByDescending(x => x.IsDiscounted)
                .ThenByDescending(x => x.CreatedAt),

            "featured" => query
                .OrderByDescending(x => x.IsFeatured)
                .ThenByDescending(x => x.CreatedAt),

            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var products = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new ProductListDto
            {
                Id = x.Id,
                Name = x.Name,
                ProductCode = x.ProductCode,
                Model = x.Model,
                Price = x.Price,
                DiscountPrice = x.DiscountPrice,
                IsDiscounted = x.IsDiscounted,
                IsFeatured = x.IsFeatured,
                CategoryName = x.Category.Name,
                BrandName = x.Brand.Name,
                MainImageUrl = x.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Order)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                TotalStock = x.Variants
                    .Where(v => v.IsActive)
                    .Sum(v => v.StockCount)
            })
            .ToListAsync();

        var result = new PagedResult<ProductListDto>
        {
            Items = products,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };

        return Ok(ApiResponse<PagedResult<ProductListDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProductDetail(Guid id)
    {
        var product = await _context.Products
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Images)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Size)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Color)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        product.ViewCount++;
        await _context.SaveChangesAsync();

        var dto = new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ProductCode = product.ProductCode,
            Model = product.Model,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            IsDiscounted = product.IsDiscounted,
            IsFeatured = product.IsFeatured,
            CategoryName = product.Category.Name,
            BrandName = product.Brand.Name,
            Images = product.Images
                .OrderByDescending(x => x.IsMain)
                .ThenBy(x => x.Order)
                .Select(x => x.ImageUrl)
                .ToList(),
            Variants = product.Variants
                .Where(v => v.IsActive)
                .Select(v => new ProductVariantDetailDto
                {
                    Id = v.Id,
                    SizeId = v.SizeId,
                    SizeValue = v.Size.Value,
                    ColorId = v.ColorId,
                    ColorName = v.Color.Name,
                    ColorHexCode = v.Color.HexCode,
                    StockCount = v.StockCount
                })
                .ToList()
        };

        return Ok(ApiResponse<ProductDetailDto>.Ok(dto));
    }

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId)
    {
        var productsQuery = _context.Products
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Size)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Color)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            productsQuery = productsQuery.Where(x => x.CategoryId == categoryId.Value);
        }

        if (brandId.HasValue)
        {
            productsQuery = productsQuery.Where(x => x.BrandId == brandId.Value);
        }

        var products = await productsQuery.ToListAsync();

        var options = new ProductFilterOptionsDto
        {
            Categories = products
                .Select(x => x.Category)
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList(),

            Brands = products
                .Select(x => x.Brand)
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    ImageUrl = x.ImageUrl
                })
                .ToList(),

            Sizes = products
                .SelectMany(x => x.Variants)
                .Where(v => v.IsActive && v.StockCount > 0)
                .Select(v => v.Size)
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.Value)
                .Select(x => new FilterOptionDto
                {
                    Id = x.Id,
                    Name = x.Value
                })
                .ToList(),

            Colors = products
                .SelectMany(x => x.Variants)
                .Where(v => v.IsActive && v.StockCount > 0)
                .Select(v => v.Color)
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.Name)
                .Select(x => new ColorFilterOptionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    HexCode = x.HexCode
                })
                .ToList(),

            Models = products
                .Where(x => !string.IsNullOrWhiteSpace(x.Model))
                .Select(x => x.Model!)
                .Distinct()
                .OrderBy(x => x)
                .ToList(),

            MinPrice = products.Any()
                ? products.Min(x => x.IsDiscounted && x.DiscountPrice.HasValue
                    ? x.DiscountPrice.Value
                    : x.Price)
                : 0,

            MaxPrice = products.Any()
                ? products.Max(x => x.IsDiscounted && x.DiscountPrice.HasValue
                    ? x.DiscountPrice.Value
                    : x.Price)
                : 0
        };

        return Ok(ApiResponse<ProductFilterOptionsDto>.Ok(options));
    }
}