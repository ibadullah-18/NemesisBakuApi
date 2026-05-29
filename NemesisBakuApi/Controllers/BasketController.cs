using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Basket;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BasketController : ControllerBase
{
    private readonly AppDbContext _context;

    public BasketController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException();

        return Guid.Parse(userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetBasket()
    {
        var userId = GetUserId();

        var items = await _context.BasketItems
            .Include(x => x.Product)
                .ThenInclude(p => p.Images)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Size)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Color)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BasketItemDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductVariantId = x.ProductVariantId,

                ProductName = x.Product.Name,
                ProductCode = x.Product.ProductCode,
                ProductImageUrl = x.Product.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Order)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),

                SizeValue = x.ProductVariant.Size.Value,
                ColorName = x.ProductVariant.Color.Name,
                ColorHexCode = x.ProductVariant.Color.HexCode,

                UnitPrice = x.Product.IsDiscounted && x.Product.DiscountPrice.HasValue
                    ? x.Product.DiscountPrice.Value
                    : x.Product.Price,

                Quantity = x.Quantity,

                TotalPrice =
                    (x.Product.IsDiscounted && x.Product.DiscountPrice.HasValue
                        ? x.Product.DiscountPrice.Value
                        : x.Product.Price) * x.Quantity,

                StockCount = x.ProductVariant.StockCount
            })
            .ToListAsync();

        var summary = new BasketSummaryDto
        {
            Items = items,
            TotalQuantity = items.Sum(x => x.Quantity),
            TotalPrice = items.Sum(x => x.TotalPrice)
        };

        return Ok(ApiResponse<BasketSummaryDto>.Ok(summary));
    }

    [HttpPost]
    public async Task<IActionResult> AddToBasket(AddToBasketDto dto)
    {
        var userId = GetUserId();

        if (dto.Quantity <= 0)
            return BadRequest(ApiResponse<string>.Fail("Miqdar düzgün deyil"));

        var product = await _context.Products
            .FirstOrDefaultAsync(x => x.Id == dto.ProductId && x.IsActive);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(x =>
                x.Id == dto.ProductVariantId &&
                x.ProductId == dto.ProductId &&
                x.IsActive);

        if (variant == null)
            return NotFound(ApiResponse<string>.Fail("Məhsulun seçilmiş razmer/rəngi tapılmadı"));

        if (variant.StockCount <= 0)
            return BadRequest(ApiResponse<string>.Fail("Bu məhsul stokda yoxdur"));

        var existingItem = await _context.BasketItems
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.ProductVariantId == dto.ProductVariantId);

        if (existingItem != null)
        {
            var newQuantity = existingItem.Quantity + dto.Quantity;

            if (newQuantity > variant.StockCount)
                return BadRequest(ApiResponse<string>.Fail("Stokda kifayət qədər məhsul yoxdur"));

            existingItem.Quantity = newQuantity;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            if (dto.Quantity > variant.StockCount)
                return BadRequest(ApiResponse<string>.Fail("Stokda kifayət qədər məhsul yoxdur"));

            var basketItem = new BasketItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                ProductVariantId = dto.ProductVariantId,
                Quantity = dto.Quantity
            };

            _context.BasketItems.Add(basketItem);
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Məhsul səbətə əlavə olundu"));
    }

    [HttpPut("{basketItemId}")]
    public async Task<IActionResult> UpdateBasketItem(Guid basketItemId, UpdateBasketItemDto dto)
    {
        var userId = GetUserId();

        if (dto.Quantity <= 0)
            return BadRequest(ApiResponse<string>.Fail("Miqdar düzgün deyil"));

        var basketItem = await _context.BasketItems
            .Include(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => x.Id == basketItemId && x.UserId == userId);

        if (basketItem == null)
            return NotFound(ApiResponse<string>.Fail("Səbət məhsulu tapılmadı"));

        if (dto.Quantity > basketItem.ProductVariant.StockCount)
            return BadRequest(ApiResponse<string>.Fail("Stokda kifayət qədər məhsul yoxdur"));

        basketItem.Quantity = dto.Quantity;
        basketItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Səbət məhsulu yeniləndi"));
    }

    [HttpDelete("{basketItemId}")]
    public async Task<IActionResult> DeleteBasketItem(Guid basketItemId)
    {
        var userId = GetUserId();

        var basketItem = await _context.BasketItems
            .FirstOrDefaultAsync(x => x.Id == basketItemId && x.UserId == userId);

        if (basketItem == null)
            return NotFound(ApiResponse<string>.Fail("Səbət məhsulu tapılmadı"));

        basketItem.IsDeleted = true;
        basketItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Məhsul səbətdən silindi"));
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearBasket()
    {
        var userId = GetUserId();

        var items = await _context.BasketItems
            .Where(x => x.UserId == userId)
            .ToListAsync();

        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Səbət təmizləndi"));
    }
}
