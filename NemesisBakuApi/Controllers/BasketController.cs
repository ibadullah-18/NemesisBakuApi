using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Basket;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BasketController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    public BasketController(
        AppDbContext context,
        IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
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

        var basketItems = await _context.BasketItems
            .Include(x => x.Product)
                .ThenInclude(p => p.Images)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Size)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Color)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var items = basketItems.Select(x =>
        {
            var originalPrice = x.Product.Price;

            var currentPrice =
                x.Product.DiscountPrice.HasValue &&
                x.Product.DiscountPrice.Value > 0 &&
                x.Product.DiscountPrice.Value < x.Product.Price
                    ? x.Product.DiscountPrice.Value
                    : x.Product.Price;

            return new BasketItemDto
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

                OriginalPrice = originalPrice,
                UnitPrice = currentPrice,
                DiscountAmount = (originalPrice - currentPrice) * x.Quantity,

                Quantity = x.Quantity,

                OriginalTotalPrice = originalPrice * x.Quantity,
                TotalPrice = currentPrice * x.Quantity,

                HasDiscount = currentPrice < originalPrice,

                StockCount = x.ProductVariant.StockCount
            };
        }).ToList();

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            foreach (var item in items)
            {
                if (item.StockCount > 0 && item.StockCount <= 3)
                {
                    var alreadySent = await _context.BasketLowStockEmailLogs
                        .AnyAsync(x =>
                            x.UserId == userId &&
                            x.ProductVariantId == item.ProductVariantId);

                    if (!alreadySent)
                    {
                        var productLink = $"https://nemesisbaku.az/products/{item.ProductId}";

                        var sent = await _emailService.SendBasketLowStockAsync(
                            user.Email,
                            item.ProductName,
                            productLink,
                            item.StockCount);

                        if (sent)
                        {
                            _context.BasketLowStockEmailLogs.Add(new BasketLowStockEmailLog
                            {
                                UserId = userId,
                                ProductId = item.ProductId,
                                ProductVariantId = item.ProductVariantId,
                                Email = user.Email,
                                StockCountAtSend = item.StockCount,
                                SentAt = DateTime.UtcNow
                            });

                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }
        }

        var summary = new BasketSummaryDto
        {
            Items = items,
            TotalQuantity = items.Sum(x => x.Quantity),
            OriginalTotalPrice = items.Sum(x => x.OriginalTotalPrice),
            TotalDiscountAmount = items.Sum(x => x.DiscountAmount),
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

        if (dto.Quantity > variant.StockCount)
            return BadRequest(ApiResponse<string>.Fail("Stokda kifayət qədər məhsul yoxdur"));

        var existingItem = await _context.BasketItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.ProductVariantId == dto.ProductVariantId);

        if (existingItem != null)
        {
            if (existingItem.IsDeleted)
            {
                existingItem.IsDeleted = false;
                existingItem.Quantity = dto.Quantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var newQuantity = existingItem.Quantity + dto.Quantity;

                if (newQuantity > variant.StockCount)
                    return BadRequest(ApiResponse<string>.Fail("Stokda kifayət qədər məhsul yoxdur"));

                existingItem.Quantity = newQuantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
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