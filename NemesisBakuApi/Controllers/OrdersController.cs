using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;
using System.Security.Claims;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly DeliverySettings _deliverySettings;
    private readonly ITelegramOrderNotificationOutbox _telegramOutbox;

    public OrdersController(
        AppDbContext context,
        IOptions<DeliverySettings> deliveryOptions,
        ITelegramOrderNotificationOutbox telegramOutbox)
    {
        _context = context;
        _deliverySettings = deliveryOptions.Value;
        _telegramOutbox = telegramOutbox;
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var userId = GetUserId();

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail("Sifariş üçün məhsul seçilməyib"));
        }

        var basketItemIds = dto.Items
            .Select(x => x.BasketItemId)
            .Distinct()
            .ToList();

        if (basketItemIds.Count != dto.Items.Count)
        {
            return BadRequest(
                ApiResponse<string>.Fail("Eyni səbət məhsulu bir neçə dəfə göndərilib"));
        }

        var storeInfo = await _context.StoreInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (storeInfo == null)
        {
            return BadRequest(
                ApiResponse<string>.Fail("Mağaza məlumatları tapılmadı"));
        }

        if (string.IsNullOrWhiteSpace(storeInfo.WhatsAppNumber))
        {
            return BadRequest(
                ApiResponse<string>.Fail("Mağaza WhatsApp nömrəsi təyin edilməyib"));
        }

        decimal deliveryPrice = 0;
        decimal? deliveryDistanceKm = null;

        if (dto.DeliveryType == DeliveryType.HomeDelivery)
        {
            if (dto.SavedAddressId.HasValue)
            {
                var savedAddress = await _context.UserAddresses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == dto.SavedAddressId.Value &&
                             x.UserId == userId,
                        cancellationToken);

                if (savedAddress == null)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail("Seçilmiş ünvan tapılmadı"));
                }

                dto.AddressText = savedAddress.AddressText;
                dto.Latitude = savedAddress.Latitude;
                dto.Longitude = savedAddress.Longitude;
                dto.BuildingNumber = savedAddress.BuildingNumber;
                dto.Floor = savedAddress.Floor;
                dto.Apartment = savedAddress.Apartment;

                if (string.IsNullOrWhiteSpace(dto.Note))
                    dto.Note = savedAddress.Note;
            }

            if (string.IsNullOrWhiteSpace(dto.AddressText))
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Ünvana çatdırılma üçün ünvan məcburidir"));
            }

            if (!dto.Latitude.HasValue || !dto.Longitude.HasValue)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Çatdırılma üçün xəritədən konum seçilməlidir"));
            }

            if (!dto.DeliveryDate.HasValue)
            {
                return BadRequest(
                    ApiResponse<string>.Fail("Çatdırılma tarixi seçilməlidir"));
            }

            if (string.IsNullOrWhiteSpace(dto.DeliveryTimeRange))
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Çatdırılma saat aralığı seçilməlidir"));
            }

            if (!storeInfo.Latitude.HasValue ||
                !storeInfo.Longitude.HasValue)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Mağaza koordinatları təyin edilməyib"));
            }

            deliveryDistanceKm =
                DeliveryPriceCalculator.CalculateDistanceKm(
                    storeInfo.Latitude.Value,
                    storeInfo.Longitude.Value,
                    dto.Latitude.Value,
                    dto.Longitude.Value);

            deliveryPrice =
                DeliveryPriceCalculator.CalculateDeliveryPrice(
                    deliveryDistanceKm.Value,
                    _deliverySettings);
        }
        else if (dto.DeliveryType == DeliveryType.PickupFromStore)
        {
            deliveryPrice = 0;
            deliveryDistanceKm = null;

            dto.AddressText = null;
            dto.Latitude = null;
            dto.Longitude = null;
            dto.BuildingNumber = null;
            dto.Floor = null;
            dto.Apartment = null;
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var basketItems = await _context.BasketItems
                .AsSplitQuery()
                .Include(x => x.Product)
                    .ThenInclude(x => x.Images)
                .Include(x => x.ProductVariant)
                    .ThenInclude(x => x.Size)
                .Include(x => x.ProductVariant)
                    .ThenInclude(x => x.Color)
                .Where(x =>
                    x.UserId == userId &&
                    basketItemIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (basketItems.Count != basketItemIds.Count)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Səbətdə seçilmiş məhsullardan biri tapılmadı"));
            }

            foreach (var basketItem in basketItems)
            {
                if (!basketItem.Product.IsActive)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail(
                            $"{basketItem.Product.Name} aktiv deyil"));
                }

                if (!basketItem.ProductVariant.IsActive)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail(
                            $"{basketItem.Product.Name} üçün seçilmiş razmer/rəng aktiv deyil"));
                }

                if (basketItem.Quantity <= 0)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail(
                            $"{basketItem.Product.Name} üçün say düzgün deyil"));
                }

                if (basketItem.ProductVariant.StockCount <
                    basketItem.Quantity)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail(
                            $"{basketItem.Product.Name} üçün stok kifayət deyil"));
                }
            }

            decimal totalProductPrice = 0;

            var order = new Order
            {
                UserId = userId,
                OrderNumber = OrderNumberGenerator.Generate(),

                CustomerFullName = dto.CustomerFullName.Trim(),
                CustomerPhoneNumber = dto.CustomerPhoneNumber.Trim(),

                DeliveryType = dto.DeliveryType,
                PaymentMethod = dto.PaymentMethod,

                AddressText = dto.AddressText?.Trim(),
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,

                BuildingNumber = dto.BuildingNumber?.Trim(),
                Floor = dto.Floor?.Trim(),
                Apartment = dto.Apartment?.Trim(),

                DeliveryDate = dto.DeliveryDate,
                DeliveryTimeRange = dto.DeliveryTimeRange?.Trim(),

                DeliveryPrice = deliveryPrice,
                DeliveryDistanceKm = deliveryDistanceKm,

                Note = dto.Note?.Trim(),
                Status = OrderStatus.Pending
            };

            foreach (var basketItem in basketItems)
            {
                var product = basketItem.Product;
                var variant = basketItem.ProductVariant;

                var unitPrice =
                    product.DiscountPrice.HasValue &&
                    product.DiscountPrice.Value > 0 &&
                    product.DiscountPrice.Value < product.Price
                        ? product.DiscountPrice.Value
                        : product.Price;

                var itemTotal = unitPrice * basketItem.Quantity;
                totalProductPrice += itemTotal;

                var mainImage = product.Images
                    .OrderByDescending(x => x.IsMain)
                    .ThenBy(x => x.Order)
                    .Select(x => x.ImageUrl)
                    .FirstOrDefault();

                var productLink =
                    $"https://nemesisbaku.az/products/{basketItem.ProductId}";

                order.Items.Add(new OrderItem
                {
                    ProductId = basketItem.ProductId,
                    ProductVariantId = basketItem.ProductVariantId,

                    ProductName = product.Name,
                    ProductCode = product.ProductCode,

                    SizeValue = variant.Size.Value,
                    ColorName = variant.Color.Name,

                    UnitPrice = unitPrice,
                    Quantity = basketItem.Quantity,
                    TotalPrice = itemTotal,

                    ProductImageUrl = mainImage,
                    ProductLink = productLink
                });

                variant.StockCount -= basketItem.Quantity;

                basketItem.IsDeleted = true;
                basketItem.UpdatedAt = DateTime.UtcNow;
            }

            decimal promoDiscountAmount = 0;

            if (!string.IsNullOrWhiteSpace(dto.PromoCode))
            {
                var now = DateTime.UtcNow;
                var normalizedCode = dto.PromoCode.Trim().ToUpperInvariant();

                var promo = await _context.PromoCodes
                    .FirstOrDefaultAsync(
                        x => x.Code == normalizedCode &&
                             x.IsActive &&
                             x.StartDate <= now &&
                             (!x.EndDate.HasValue ||
                              x.EndDate.Value >= now),
                        cancellationToken);

                if (promo == null)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail(
                            "Promo kod yanlışdır və ya aktiv deyil"));
                }

                if (promo.UsageLimit.HasValue &&
                    promo.UsedCount >= promo.UsageLimit.Value)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail(
                            "Promo kod istifadə limitinə çatıb"));
                }

                if (promo.MinOrderAmount.HasValue &&
                    totalProductPrice < promo.MinOrderAmount.Value)
                {
                    return BadRequest(
                        ApiResponse<string>.Fail(
                            $"Bu promo kod üçün minimum sifariş məbləği {promo.MinOrderAmount.Value} AZN olmalıdır"));
                }

                promoDiscountAmount =
                    promo.DiscountType == DiscountType.Percentage
                        ? totalProductPrice * promo.DiscountValue / 100
                        : promo.DiscountValue;

                if (promoDiscountAmount > totalProductPrice)
                    promoDiscountAmount = totalProductPrice;

                if (promoDiscountAmount < 0)
                    promoDiscountAmount = 0;

                promo.UsedCount++;

                _context.PromoCodeUsages.Add(new PromoCodeUsage
                {
                    PromoCodeId = promo.Id,
                    UserId = userId,
                    Order = order,
                    DiscountAmount = promoDiscountAmount
                });
            }

            order.TotalProductPrice = totalProductPrice;
            order.PromoDiscountAmount = promoDiscountAmount;
            order.TotalPrice =
                totalProductPrice -
                promoDiscountAmount +
                order.DeliveryPrice;

            if (dto.DeliveryType == DeliveryType.HomeDelivery &&
                dto.SaveAddressToProfile &&
                !dto.SavedAddressId.HasValue &&
                !string.IsNullOrWhiteSpace(dto.AddressText) &&
                dto.Latitude.HasValue &&
                dto.Longitude.HasValue)
            {
                var address = new UserAddress
                {
                    UserId = userId,
                    Title = string.IsNullOrWhiteSpace(dto.AddressTitle)
                        ? "Ünvan"
                        : dto.AddressTitle.Trim(),

                    AddressText = dto.AddressText.Trim(),
                    Latitude = dto.Latitude.Value,
                    Longitude = dto.Longitude.Value,

                    BuildingNumber = dto.BuildingNumber?.Trim(),
                    Floor = dto.Floor?.Trim(),
                    Apartment = dto.Apartment?.Trim(),
                    Note = dto.Note?.Trim(),

                    IsDefault = false
                };

                _context.UserAddresses.Add(address);
            }

            _context.Orders.Add(order);

            await _telegramOutbox.EnqueueAsync(
                order,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(
                ApiResponse<Guid>.Ok(
                    order.Id,
                    "Sifariş uğurla yaradıldı"));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);

            return Conflict(
                ApiResponse<string>.Fail(
                    "Stok və ya promo kod məlumatı dəyişdi. Səbəti yeniləyib yenidən yoxlayın"));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        limit = Math.Clamp(limit, 1, 200);

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new OrderListDto
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                TotalPrice = x.TotalPrice,
                DeliveryType = x.DeliveryType,
                PaymentMethod = x.PaymentMethod,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<OrderListDto>>.Ok(orders));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var result = await _context.Orders
            .AsNoTracking()
            .Where(x =>
                x.Id == id &&
                x.UserId == userId)
            .Select(order => new OrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerFullName = order.CustomerFullName,
                CustomerPhoneNumber = order.CustomerPhoneNumber,
                DeliveryType = order.DeliveryType,
                PaymentMethod = order.PaymentMethod,
                AddressText = order.AddressText,
                Latitude = order.Latitude,
                Longitude = order.Longitude,
                BuildingNumber = order.BuildingNumber,
                Floor = order.Floor,
                Apartment = order.Apartment,
                DeliveryDate = order.DeliveryDate,
                DeliveryTimeRange = order.DeliveryTimeRange,
                DeliveryPrice = order.DeliveryPrice,
                DeliveryDistanceKm = order.DeliveryDistanceKm,
                Note = order.Note,
                TotalProductPrice = order.TotalProductPrice,
                PromoDiscountAmount = order.PromoDiscountAmount,
                TotalPrice = order.TotalPrice,
                Status = order.Status,
                IsWhatsappMessageSent = order.IsWhatsappMessageSent,
                WhatsappMessageSentAt = order.WhatsappMessageSentAt,
                CreatedAt = order.CreatedAt,
                Items = order.Items
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => new OrderItemDto
                    {
                        ProductId = x.ProductId,
                        ProductVariantId = x.ProductVariantId,
                        ProductName = x.ProductName,
                        ProductCode = x.ProductCode,
                        SizeValue = x.SizeValue,
                        ColorName = x.ColorName,
                        UnitPrice = x.UnitPrice,
                        Quantity = x.Quantity,
                        TotalPrice = x.TotalPrice,
                        ProductImageUrl = x.ProductImageUrl,
                        ProductLink = x.ProductLink
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            return NotFound(
                ApiResponse<string>.Fail("Sifariş tapılmadı"));
        }

        return Ok(ApiResponse<OrderDetailDto>.Ok(result));
    }

    [HttpPost("calculate-delivery")]
    public async Task<IActionResult> CalculateDelivery(
        CalculateDeliveryDto dto)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (dto.Latitude < -90 || dto.Latitude > 90)
        {
            return BadRequest(
                ApiResponse<string>.Fail("Latitude düzgün deyil"));
        }

        if (dto.Longitude < -180 || dto.Longitude > 180)
        {
            return BadRequest(
                ApiResponse<string>.Fail("Longitude düzgün deyil"));
        }

        var storeInfo = await _context.StoreInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (storeInfo == null)
        {
            return BadRequest(
                ApiResponse<string>.Fail("Mağaza məlumatları tapılmadı"));
        }

        if (!storeInfo.Latitude.HasValue ||
            !storeInfo.Longitude.HasValue)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Mağaza koordinatları təyin edilməyib"));
        }

        var distanceKm =
            DeliveryPriceCalculator.CalculateDistanceKm(
                storeInfo.Latitude.Value,
                storeInfo.Longitude.Value,
                dto.Latitude,
                dto.Longitude);

        var deliveryPrice =
            DeliveryPriceCalculator.CalculateDeliveryPrice(
                distanceKm,
                _deliverySettings);

        var result = new CalculateDeliveryResultDto
        {
            DistanceKm = distanceKm,
            DeliveryPrice = deliveryPrice
        };

        return Ok(
            ApiResponse<CalculateDeliveryResultDto>.Ok(result));
    }
}