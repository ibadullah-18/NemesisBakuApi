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
         .AsNoTracking()
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
    public async Task<IActionResult> GetProductDetail(
    Guid id,
    CancellationToken cancellationToken)
    {
        var updatedRows = await _context.Products
            .Where(x => x.Id == id && x.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.ViewCount,
                    x => x.ViewCount + 1),
                cancellationToken);

        if (updatedRows == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail("Məhsul tapılmadı"));
        }

        var product = await _context.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Images)
            .Include(x => x.Variants)
                .ThenInclude(x => x.Size)
            .Include(x => x.Variants)
                .ThenInclude(x => x.Color)
            .FirstAsync(
                x => x.Id == id && x.IsActive,
                cancellationToken);

        var dto = new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ProductCode = product.ProductCode,
            Model = product.Model,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,

            IsDiscounted =
                product.DiscountPrice.HasValue &&
                product.DiscountPrice.Value > 0 &&
                product.DiscountPrice.Value < product.Price,

            IsFeatured = product.IsFeatured,
            CategoryName = product.Category.Name,
            BrandName = product.Brand.Name,

            Images = product.Images
                .OrderByDescending(x => x.IsMain)
                .ThenBy(x => x.Order)
                .Select(x => new ProductImageDetailDto
                {
                    Id = x.Id,
                    ImageUrl = x.ImageUrl,
                    IsMain = x.IsMain,
                    DisplayOrder = x.Order
                })
                .ToList(),

            Variants = product.Variants
                .Where(x => x.IsActive)
                .Select(x => new ProductVariantDetailDto
                {
                    Id = x.Id,
                    SizeId = x.SizeId,
                    SizeValue = x.Size.Value,
                    ColorId = x.ColorId,
                    ColorName = x.Color.Name,
                    ColorHexCode = x.Color.HexCode,
                    StockCount = x.StockCount
                })
                .ToList()
        };

        return Ok(ApiResponse<ProductDetailDto>.Ok(dto));
    }

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken)
    {
        var productsQuery = _context.Products
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (categoryId.HasValue)
        {
            productsQuery = productsQuery.Where(
                x => x.CategoryId == categoryId.Value);
        }

        if (brandId.HasValue)
        {
            productsQuery = productsQuery.Where(
                x => x.BrandId == brandId.Value);
        }

        var categories = await productsQuery
            .Select(x => new
            {
                x.Category.Id,
                x.Category.Name
            })
            .Distinct()
            .OrderBy(x => x.Name)
            .Select(x => new FilterOptionDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);

        var brands = await productsQuery
            .Select(x => new
            {
                x.Brand.Id,
                x.Brand.Name,
                x.Brand.ImageUrl
            })
            .Distinct()
            .OrderBy(x => x.Name)
            .Select(x => new FilterOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                ImageUrl = x.ImageUrl
            })
            .ToListAsync(cancellationToken);

        var sizes = await _context.ProductVariants
            .AsNoTracking()
            .Where(variant =>
                variant.IsActive &&
                variant.StockCount > 0 &&
                productsQuery.Any(product =>
                    product.Id == variant.ProductId))
            .Select(variant => new
            {
                variant.Size.Id,
                Name = variant.Size.Value
            })
            .Distinct()
            .OrderBy(x => x.Name)
            .Select(x => new FilterOptionDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);

        var colors = await _context.ProductVariants
            .AsNoTracking()
            .Where(variant =>
                variant.IsActive &&
                variant.StockCount > 0 &&
                productsQuery.Any(product =>
                    product.Id == variant.ProductId))
            .Select(variant => new
            {
                variant.Color.Id,
                variant.Color.Name,
                variant.Color.HexCode
            })
            .Distinct()
            .OrderBy(x => x.Name)
            .Select(x => new ColorFilterOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                HexCode = x.HexCode
            })
            .ToListAsync(cancellationToken);

        var models = await productsQuery
            .Where(x => x.Model != null && x.Model != "")
            .Select(x => x.Model!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var priceRange = await productsQuery
            .Select(x => new
            {
                EffectivePrice =
                    x.IsDiscounted &&
                    x.DiscountPrice.HasValue
                        ? x.DiscountPrice.Value
                        : x.Price
            })
            .GroupBy(x => 1)
            .Select(group => new
            {
                MinPrice = group.Min(x => x.EffectivePrice),
                MaxPrice = group.Max(x => x.EffectivePrice)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var options = new ProductFilterOptionsDto
        {
            Categories = categories,
            Brands = brands,
            Sizes = sizes,
            Colors = colors,
            Models = models,
            MinPrice = priceRange?.MinPrice ?? 0,
            MaxPrice = priceRange?.MaxPrice ?? 0
        };

        return Ok(
            ApiResponse<ProductFilterOptionsDto>.Ok(options));
    }
}