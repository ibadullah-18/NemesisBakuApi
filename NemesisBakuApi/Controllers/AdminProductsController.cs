using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Product;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminProductsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductCreateDto dto)
    {
        var categoryExists = await _context.Categories.AnyAsync(x => x.Id == dto.CategoryId);
        if (!categoryExists)
            return BadRequest(ApiResponse<string>.Fail("Kateqoriya tapılmadı"));

        var brandExists = await _context.Brands.AnyAsync(x => x.Id == dto.BrandId);
        if (!brandExists)
            return BadRequest(ApiResponse<string>.Fail("Brend tapılmadı"));

        var codeExists = await _context.Products.AnyAsync(x => x.ProductCode == dto.ProductCode);
        if (codeExists)
            return BadRequest(ApiResponse<string>.Fail("Bu məhsul kodu artıq mövcuddur"));

        if (dto.Variants == null || !dto.Variants.Any())
            return BadRequest(ApiResponse<string>.Fail("Ən azı bir razmer/rəng/stok əlavə olunmalıdır"));

        foreach (var variant in dto.Variants)
        {
            var sizeExists = await _context.Sizes.AnyAsync(x => x.Id == variant.SizeId);
            if (!sizeExists)
                return BadRequest(ApiResponse<string>.Fail("Razmer tapılmadı"));

            var colorExists = await _context.Colors.AnyAsync(x => x.Id == variant.ColorId);
            if (!colorExists)
                return BadRequest(ApiResponse<string>.Fail("Rəng tapılmadı"));

            if (variant.StockCount < 0)
                return BadRequest(ApiResponse<string>.Fail("Stok sayı mənfi ola bilməz"));
        }

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            ProductCode = dto.ProductCode,
            Model = dto.Model,
            Price = dto.Price,
            DiscountPrice = dto.DiscountPrice,
            IsDiscounted = dto.IsDiscounted,
            IsFeatured = dto.IsFeatured,
            CategoryId = dto.CategoryId,
            BrandId = dto.BrandId,
            Variants = dto.Variants.Select(v => new ProductVariant
            {
                SizeId = v.SizeId,
                ColorId = v.ColorId,
                StockCount = v.StockCount
            }).ToList()
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(product.Id, "Məhsul uğurla əlavə olundu"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _context.Products
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Images)
            .Include(x => x.Variants)
            .OrderByDescending(x => x.CreatedAt)
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
                    .Where(i => i.IsMain)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                TotalStock = x.Variants.Sum(v => v.StockCount)
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ProductListDto>>.Ok(products));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _context.Products
            .Include(x => x.Category)
            .Include(x => x.Brand)
            .Include(x => x.Images)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Size)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Color)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

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
                .OrderBy(x => x.Order)
                .Select(x => x.ImageUrl)
                .ToList(),
            Variants = product.Variants.Select(v => new ProductVariantDetailDto
            {
                Id = v.Id,
                SizeId = v.SizeId,
                SizeValue = v.Size.Value,
                ColorId = v.ColorId,
                ColorName = v.Color.Name,
                ColorHexCode = v.Color.HexCode,
                StockCount = v.StockCount
            }).ToList()
        };

        return Ok(ApiResponse<ProductDetailDto>.Ok(dto));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, ProductUpdateDto dto)
    {
        var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == id);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        var categoryExists = await _context.Categories.AnyAsync(x => x.Id == dto.CategoryId);
        if (!categoryExists)
            return BadRequest(ApiResponse<string>.Fail("Kateqoriya tapılmadı"));

        var brandExists = await _context.Brands.AnyAsync(x => x.Id == dto.BrandId);
        if (!brandExists)
            return BadRequest(ApiResponse<string>.Fail("Brend tapılmadı"));

        var codeExists = await _context.Products
            .AnyAsync(x => x.ProductCode == dto.ProductCode && x.Id != id);

        if (codeExists)
            return BadRequest(ApiResponse<string>.Fail("Bu məhsul kodu artıq mövcuddur"));

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.ProductCode = dto.ProductCode;
        product.Model = dto.Model;
        product.Price = dto.Price;
        product.DiscountPrice = dto.DiscountPrice;
        product.IsDiscounted = dto.IsDiscounted;
        product.IsFeatured = dto.IsFeatured;
        product.IsActive = dto.IsActive;
        product.CategoryId = dto.CategoryId;
        product.BrandId = dto.BrandId;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Məhsul uğurla yeniləndi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == id);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Məhsul uğurla silindi"));
    }
}