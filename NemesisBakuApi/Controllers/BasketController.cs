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
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> GetBasket(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var items = await _context.BasketItems
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BasketItemDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductVariantId =
                    x.ProductVariantId,

                ProductName = x.Product.Name,
                ProductCode =
                    x.Product.ProductCode,

                ProductImageUrl =
                    x.Product.Images
                        .OrderByDescending(
                            image => image.IsMain)
                        .ThenBy(
                            image => image.Order)
                        .Select(
                            image => image.ImageUrl)
                        .FirstOrDefault(),

                SizeValue =
                    x.ProductVariant.Size.Value,

                ColorName =
                    x.ProductVariant.Color.Name,

                ColorHexCode =
                    x.ProductVariant.Color.HexCode,

                OriginalPrice =
                    x.Product.Price,

                UnitPrice =
                    x.Product.DiscountPrice.HasValue &&
                    x.Product.DiscountPrice.Value > 0 &&
                    x.Product.DiscountPrice.Value <
                    x.Product.Price
                        ? x.Product.DiscountPrice.Value
                        : x.Product.Price,

                DiscountAmount =
                    (x.Product.Price -
                     (x.Product.DiscountPrice.HasValue &&
                      x.Product.DiscountPrice.Value > 0 &&
                      x.Product.DiscountPrice.Value <
                      x.Product.Price
                         ? x.Product.DiscountPrice.Value
                         : x.Product.Price))
                    * x.Quantity,

                Quantity = x.Quantity,

                OriginalTotalPrice =
                    x.Product.Price * x.Quantity,

                TotalPrice =
                    (x.Product.DiscountPrice.HasValue &&
                     x.Product.DiscountPrice.Value > 0 &&
                     x.Product.DiscountPrice.Value <
                     x.Product.Price
                        ? x.Product.DiscountPrice.Value
                        : x.Product.Price)
                    * x.Quantity,

                HasDiscount =
                    x.Product.DiscountPrice.HasValue &&
                    x.Product.DiscountPrice.Value > 0 &&
                    x.Product.DiscountPrice.Value <
                    x.Product.Price,

                StockCount =
                    x.ProductVariant.StockCount
            })
            .ToListAsync(cancellationToken);

        await SendLowStockEmailsAsync(
            userId,
            items,
            cancellationToken);

        var summary = new BasketSummaryDto
        {
            Items = items,

            TotalQuantity =
                items.Sum(x => x.Quantity),

            OriginalTotalPrice =
                items.Sum(x => x.OriginalTotalPrice),

            TotalDiscountAmount =
                items.Sum(x => x.DiscountAmount),

            TotalPrice =
                items.Sum(x => x.TotalPrice)
        };

        return Ok(
            ApiResponse<BasketSummaryDto>.Ok(summary));
    }

    [HttpPost]
    public async Task<IActionResult> AddToBasket(
        AddToBasketDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (dto.Quantity <= 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Miqdar düzgün deyil"));
        }

        var productExists = await _context.Products
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id == dto.ProductId &&
                    x.IsActive,
                cancellationToken);

        if (!productExists)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Məhsul tapılmadı"));
        }

        var variant = await _context.ProductVariants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == dto.ProductVariantId &&
                    x.ProductId == dto.ProductId &&
                    x.IsActive,
                cancellationToken);

        if (variant == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Məhsulun seçilmiş " +
                    "razmer/rəngi tapılmadı"));
        }

        if (variant.StockCount <= 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu məhsul stokda yoxdur"));
        }

        if (dto.Quantity > variant.StockCount)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Stokda kifayət qədər " +
                    "məhsul yoxdur"));
        }

        var existingItem =
            await _context.BasketItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    x =>
                        x.UserId == userId &&
                        x.ProductVariantId ==
                        dto.ProductVariantId,
                    cancellationToken);

        var updatedAt = DateTime.UtcNow;

        if (existingItem == null)
        {
            _context.BasketItems.Add(
                new BasketItem
                {
                    UserId = userId,
                    ProductId = dto.ProductId,

                    ProductVariantId =
                        dto.ProductVariantId,

                    Quantity = dto.Quantity
                });
        }
        else if (existingItem.IsDeleted)
        {
            existingItem.IsDeleted = false;
            existingItem.ProductId = dto.ProductId;
            existingItem.Quantity = dto.Quantity;
            existingItem.UpdatedAt = updatedAt;
        }
        else
        {
            var newQuantity =
                existingItem.Quantity +
                dto.Quantity;

            if (newQuantity > variant.StockCount)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Stokda kifayət qədər " +
                        "məhsul yoxdur"));
            }

            existingItem.Quantity = newQuantity;
            existingItem.UpdatedAt = updatedAt;
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Məhsul səbətə əlavə olundu"));
    }

    [HttpPut("{basketItemId:guid}")]
    public async Task<IActionResult> UpdateBasketItem(
        Guid basketItemId,
        UpdateBasketItemDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (dto.Quantity <= 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Miqdar düzgün deyil"));
        }

        var basketItem =
            await _context.BasketItems
                .Include(x => x.ProductVariant)
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == basketItemId &&
                        x.UserId == userId,
                    cancellationToken);

        if (basketItem == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Səbət məhsulu tapılmadı"));
        }

        if (!basketItem.ProductVariant.IsActive)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Məhsul variantı aktiv deyil"));
        }

        if (dto.Quantity >
            basketItem.ProductVariant.StockCount)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Stokda kifayət qədər " +
                    "məhsul yoxdur"));
        }

        basketItem.Quantity = dto.Quantity;
        basketItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Səbət məhsulu yeniləndi"));
    }

    [HttpDelete("{basketItemId:guid}")]
    public async Task<IActionResult> DeleteBasketItem(
        Guid basketItemId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var basketItem =
            await _context.BasketItems
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == basketItemId &&
                        x.UserId == userId,
                    cancellationToken);

        if (basketItem == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Səbət məhsulu tapılmadı"));
        }

        basketItem.IsDeleted = true;
        basketItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Məhsul səbətdən silindi"));
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearBasket(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var updatedAt = DateTime.UtcNow;

        await _context.BasketItems
            .Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.IsDeleted,
                        true)
                    .SetProperty(
                        x => x.UpdatedAt,
                        updatedAt),
                cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Səbət təmizləndi"));
    }

    private async Task SendLowStockEmailsAsync(
        Guid userId,
        IReadOnlyCollection<BasketItemDto> items,
        CancellationToken cancellationToken)
    {
        var lowStockItems = items
            .Where(x =>
                x.StockCount > 0 &&
                x.StockCount <= 3)
            .ToList();

        if (lowStockItems.Count == 0)
        {
            return;
        }

        var userEmail = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return;
        }

        var variantIds = lowStockItems
            .Select(x => x.ProductVariantId)
            .Distinct()
            .ToList();

        var sentVariantIds = await _context
    .BasketLowStockEmailLogs
    .AsNoTracking()
    .Where(x =>
        x.UserId == userId &&
        variantIds.Contains(
            x.ProductVariantId))
    .Select(x => x.ProductVariantId)
    .ToListAsync(cancellationToken);

        var alreadySentVariantIds =
            sentVariantIds.ToHashSet();

        var addedLog = false;

        foreach (var item in lowStockItems)
        {
            if (alreadySentVariantIds.Contains(
                    item.ProductVariantId))
            {
                continue;
            }

            var productLink =
                "https://nemesisbaku.az/products/" +
                item.ProductId;

            var sent =
                await _emailService
                    .SendBasketLowStockAsync(
                        userEmail,
                        item.ProductName,
                        productLink,
                        item.StockCount);

            if (!sent)
            {
                continue;
            }

            _context.BasketLowStockEmailLogs.Add(
                new BasketLowStockEmailLog
                {
                    UserId = userId,
                    ProductId = item.ProductId,

                    ProductVariantId =
                        item.ProductVariantId,

                    Email = userEmail,

                    StockCountAtSend =
                        item.StockCount,

                    SentAt = DateTime.UtcNow
                });

            alreadySentVariantIds.Add(
                item.ProductVariantId);

            addedLog = true;
        }

        if (addedLog)
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}