using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Product;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminProductsController : ControllerBase
{
    private static readonly Regex SizeNumberRegex = new(
        @"\d+(?:[.,]\d+)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppDbContext _context;
    private readonly IFileService _fileService;
    private readonly IAuditLogService _auditLogService;

    public AdminProductsController(
     AppDbContext context,
     IFileService fileService,
     IAuditLogService auditLogService)
    {
        _context = context;
        _fileService = fileService;
        _auditLogService = auditLogService;
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

        await WriteAuditLogAsync(
            "Create",
            "Product",
            product.Id.ToString(),
            $"Məhsul yaradıldı: {product.Name}");

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
                .OrderBy(v => GetNumericSize(v.Size.Value))
                .ThenBy(v => v.Color.Name)
                .ThenBy(v => v.Id)
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

        await WriteAuditLogAsync(
            "Update",
            "Product",
            product.Id.ToString(),
            $"Məhsul yeniləndi: {product.Name}");

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

        await WriteAuditLogAsync(
            "Delete",
            "Product",
            product.Id.ToString(),
            $"Məhsul silindi: {product.Name}");

        return Ok(ApiResponse<string>.Ok("Məhsul uğurla silindi"));
    }

    [HttpPost("{productId}/images")]
    public async Task<IActionResult> AddImage(Guid productId, IFormFile file, [FromQuery] bool isMain = false)
    {
        var product = await _context.Products
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == productId);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        var imageUrl = await _fileService.UploadImageAsync(file, "products");

        if (isMain || !product.Images.Any())
        {
            foreach (var img in product.Images)
            {
                img.IsMain = false;
            }
        }

        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = imageUrl,
            IsMain = isMain || !product.Images.Any(),
            Order = product.Images.Count + 1
        };

        _context.ProductImages.Add(image);
        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "AddImage",
            "ProductImage",
            image.Id.ToString(),
            $"Məhsula şəkil əlavə olundu. ProductId: {productId}");

        return Ok(ApiResponse<object>.Ok(new
        {
            image.Id,
            image.ImageUrl,
            image.IsMain,
            DisplayOrder = image.Order
        }, "Şəkil məhsula əlavə olundu"));
    }

    [HttpDelete("images/{imageId}")]
    public async Task<IActionResult> DeleteImage(Guid imageId)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(x => x.Id == imageId);

        if (image == null)
            return NotFound(ApiResponse<string>.Fail("Şəkil tapılmadı"));

        await _fileService.DeleteImageAsync(image.ImageUrl);

        image.IsDeleted = true;
        image.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "DeleteImage",
            "ProductImage",
            image.Id.ToString(),
            $"Məhsul şəkli silindi. ProductId: {image.ProductId}");

        return Ok(ApiResponse<string>.Ok("Şəkil silindi"));
    }

    [HttpPut("images/{imageId}/set-main")]
    public async Task<IActionResult> SetMainImage(Guid imageId)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(x => x.Id == imageId);

        if (image == null)
            return NotFound(ApiResponse<string>.Fail("Şəkil tapılmadı"));

        var productImages = await _context.ProductImages
            .Where(x => x.ProductId == image.ProductId)
            .ToListAsync();

        foreach (var img in productImages)
        {
            img.IsMain = false;
        }

        image.IsMain = true;
        image.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "SetMainImage",
            "ProductImage",
            image.Id.ToString(),
            $"Əsas məhsul şəkli dəyişdirildi. ProductId: {image.ProductId}");

        return Ok(ApiResponse<string>.Ok("Əsas şəkil dəyişdirildi"));
    }

    [HttpPut("images/{imageId}/order")]
    public async Task<IActionResult> UpdateImageOrder(Guid imageId, ProductImageOrderDto dto)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(x => x.Id == imageId);

        if (image == null)
            return NotFound(ApiResponse<string>.Fail("Şəkil tapılmadı"));

        if (dto.Order < 0)
            return BadRequest(ApiResponse<string>.Fail("Sıra düzgün deyil"));

        image.Order = dto.Order;
        image.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Şəkil sırası yeniləndi"));
    }

    [HttpPost("{productId}/variants")]
    public async Task<IActionResult> AddVariant(Guid productId, ProductVariantCreateDto dto)
    {
        var productExists = await _context.Products.AnyAsync(x => x.Id == productId);

        if (!productExists)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        var sizeExists = await _context.Sizes.AnyAsync(x => x.Id == dto.SizeId);

        if (!sizeExists)
            return BadRequest(ApiResponse<string>.Fail("Razmer tapılmadı"));

        var colorExists = await _context.Colors.AnyAsync(x => x.Id == dto.ColorId);

        if (!colorExists)
            return BadRequest(ApiResponse<string>.Fail("Rəng tapılmadı"));

        if (dto.StockCount < 0)
            return BadRequest(ApiResponse<string>.Fail("Stok sayı mənfi ola bilməz"));

        var exists = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.ProductId == productId &&
                x.SizeId == dto.SizeId &&
                x.ColorId == dto.ColorId);

        if (exists != null && !exists.IsDeleted)
            return BadRequest(ApiResponse<string>.Fail("Bu razmer və rəng artıq mövcuddur"));

        if (exists != null && exists.IsDeleted)
        {
            exists.IsDeleted = false;
            exists.IsActive = true;
            exists.StockCount = dto.StockCount;
            exists.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await WriteAuditLogAsync(
                "AddVariant",
                "ProductVariant",
                exists.Id.ToString(),
                $"Məhsula variant əlavə olundu. ProductId: {productId}");

            return Ok(ApiResponse<Guid>.Ok(exists.Id, "Variant yenidən aktiv edildi"));
        }

        var variant = new ProductVariant
        {
            ProductId = productId,
            SizeId = dto.SizeId,
            ColorId = dto.ColorId,
            StockCount = dto.StockCount,
            IsActive = true
        };

        _context.ProductVariants.Add(variant);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(variant.Id, "Variant əlavə olundu"));
    }

    [HttpPut("variants/{variantId}")]
    public async Task<IActionResult> UpdateVariant(Guid variantId, ProductVariantUpdateDto dto)
    {
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(x => x.Id == variantId);

        if (variant == null)
            return NotFound(ApiResponse<string>.Fail("Variant tapılmadı"));

        var sizeExists = await _context.Sizes.AnyAsync(x => x.Id == dto.SizeId);

        if (!sizeExists)
            return BadRequest(ApiResponse<string>.Fail("Razmer tapılmadı"));

        var colorExists = await _context.Colors.AnyAsync(x => x.Id == dto.ColorId);

        if (!colorExists)
            return BadRequest(ApiResponse<string>.Fail("Rəng tapılmadı"));

        if (dto.StockCount < 0)
            return BadRequest(ApiResponse<string>.Fail("Stok sayı mənfi ola bilməz"));

        var duplicateExists = await _context.ProductVariants
            .AnyAsync(x =>
                x.Id != variantId &&
                x.ProductId == variant.ProductId &&
                x.SizeId == dto.SizeId &&
                x.ColorId == dto.ColorId);

        if (duplicateExists)
            return BadRequest(ApiResponse<string>.Fail("Bu razmer və rəng artıq mövcuddur"));

        variant.SizeId = dto.SizeId;
        variant.ColorId = dto.ColorId;
        variant.StockCount = dto.StockCount;
        variant.IsActive = dto.IsActive;
        variant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "UpdateVariant",
            "ProductVariant",
            variant.Id.ToString(),
            $"Məhsul variantı yeniləndi. ProductId: {variant.ProductId}");

        return Ok(ApiResponse<string>.Ok("Variant yeniləndi"));
    }

    [HttpDelete("variants/{variantId}")]
    public async Task<IActionResult> DeleteVariant(Guid variantId)
    {
        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(x => x.Id == variantId);

        if (variant == null)
            return NotFound(ApiResponse<string>.Fail("Variant tapılmadı"));

        variant.IsDeleted = true;
        variant.IsActive = false;
        variant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "DeleteVariant",
            "ProductVariant",
            variant.Id.ToString(),
            $"Məhsul variantı silindi. ProductId: {variant.ProductId}");

        return Ok(ApiResponse<string>.Ok("Variant silindi"));
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockProducts([FromQuery] int threshold = 2)
    {
        if (threshold < 1)
            threshold = 2;

        var items = await _context.ProductVariants
            .Include(x => x.Product)
                .ThenInclude(p => p.Images)
            .Include(x => x.Size)
            .Include(x => x.Color)
            .Where(x =>
                x.IsActive &&
                x.StockCount > 0 &&
                x.StockCount <= threshold)
            .OrderBy(x => x.StockCount)
            .Select(x => new LowStockProductDto
            {
                ProductId = x.ProductId,
                VariantId = x.Id,

                ProductName = x.Product.Name,
                ProductCode = x.Product.ProductCode,

                SizeValue = x.Size.Value,
                ColorName = x.Color.Name,

                StockCount = x.StockCount,

                MainImageUrl = x.Product.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Order)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<LowStockProductDto>>.Ok(items));
    }

    private static decimal GetNumericSize(string? sizeValue)
    {
        if (string.IsNullOrWhiteSpace(sizeValue))
            return decimal.MaxValue;

        var match = SizeNumberRegex.Match(sizeValue);

        if (!match.Success)
            return decimal.MaxValue;

        var normalizedValue = match.Value.Replace(',', '.');

        return decimal.TryParse(
            normalizedValue,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var numericSize)
            ? numericSize
            : decimal.MaxValue;
    }

    private Guid? GetUserIdOrNull()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return Guid.Parse(userId);
    }

    private async Task WriteAuditLogAsync(
        string action,
        string entityName,
        string? entityId,
        string? description)
    {
        await _auditLogService.CreateAsync(
            GetUserIdOrNull(),
            action,
            entityName,
            entityId,
            description,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}