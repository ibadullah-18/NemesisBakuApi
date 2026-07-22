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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException();

        return Guid.Parse(userId);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        var userId = GetUserId();

        if (dto.Items == null || !dto.Items.Any())
            return BadRequest(ApiResponse<string>.Fail("Sifariş üçün məhsul seçilməyib"));

        var storeInfo = await _context.StoreInfos.FirstOrDefaultAsync();

        if (storeInfo == null)
            return BadRequest(ApiResponse<string>.Fail("Mağaza məlumatları tapılmadı"));

        if (string.IsNullOrWhiteSpace(storeInfo.WhatsAppNumber))
            return BadRequest(ApiResponse<string>.Fail("Mağaza WhatsApp nömrəsi təyin edilməyib"));

        decimal deliveryPrice = 0;
        decimal? deliveryDistanceKm = null;

        if (dto.DeliveryType == DeliveryType.HomeDelivery)
        {
            if (dto.SavedAddressId.HasValue)
            {
                var savedAddress = await _context.UserAddresses
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.SavedAddressId.Value &&
                        x.UserId == userId);

                if (savedAddress == null)
                    return BadRequest(ApiResponse<string>.Fail("Seçilmiş ünvan tapılmadı"));

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
                return BadRequest(ApiResponse<string>.Fail("Ünvana çatdırılma üçün ünvan məcburidir"));

            if (!dto.Latitude.HasValue || !dto.Longitude.HasValue)
                return BadRequest(ApiResponse<string>.Fail("Çatdırılma üçün xəritədən konum seçilməlidir"));

            if (!dto.DeliveryDate.HasValue)
                return BadRequest(ApiResponse<string>.Fail("Çatdırılma tarixi seçilməlidir"));

            if (string.IsNullOrWhiteSpace(dto.DeliveryTimeRange))
                return BadRequest(ApiResponse<string>.Fail("Çatdırılma saat aralığı seçilməlidir"));

            if (!storeInfo.Latitude.HasValue || !storeInfo.Longitude.HasValue)
                return BadRequest(ApiResponse<string>.Fail("Mağaza koordinatları təyin edilməyib"));

            deliveryDistanceKm = DeliveryPriceCalculator.CalculateDistanceKm(
                storeInfo.Latitude.Value,
                storeInfo.Longitude.Value,
                dto.Latitude.Value,
                dto.Longitude.Value);

            deliveryPrice = DeliveryPriceCalculator.CalculateDeliveryPrice(
                deliveryDistanceKm.Value,
                _deliverySettings);
        }

        if (dto.DeliveryType == DeliveryType.PickupFromStore)
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

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var basketItemIds = dto.Items.Select(x => x.BasketItemId).ToList();

        var basketItems = await _context.BasketItems
            .Include(x => x.Product)
                .ThenInclude(p => p.Images)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Size)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Color)
            .Where(x =>
                x.UserId == userId &&
                basketItemIds.Contains(x.Id))
            .ToListAsync();

        if (basketItems.Count != basketItemIds.Count)
            return BadRequest(ApiResponse<string>.Fail("Səbətdə seçilmiş məhsullardan biri tapılmadı"));

        foreach (var item in basketItems)
        {
            if (!item.Product.IsActive)
                return BadRequest(ApiResponse<string>.Fail($"{item.Product.Name} aktiv deyil"));

            if (!item.ProductVariant.IsActive)
                return BadRequest(ApiResponse<string>.Fail($"{item.Product.Name} üçün seçilmiş razmer/rəng aktiv deyil"));

            if (item.ProductVariant.StockCount < item.Quantity)
                return BadRequest(ApiResponse<string>.Fail($"{item.Product.Name} üçün stok kifayət deyil"));
        }

        decimal totalProductPrice = 0;

        var order = new Order
        {
            UserId = userId,
            OrderNumber = OrderNumberGenerator.Generate(),

            CustomerFullName = dto.CustomerFullName,
            CustomerPhoneNumber = dto.CustomerPhoneNumber,

            DeliveryType = dto.DeliveryType,
            PaymentMethod = dto.PaymentMethod,

            AddressText = dto.AddressText,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,

            BuildingNumber = dto.BuildingNumber,
            Floor = dto.Floor,
            Apartment = dto.Apartment,

            DeliveryDate = dto.DeliveryDate,
            DeliveryTimeRange = dto.DeliveryTimeRange,

            DeliveryPrice = deliveryPrice,
            DeliveryDistanceKm = deliveryDistanceKm,

            Note = dto.Note,

            Status = OrderStatus.Pending
        };

        foreach (var basketItem in basketItems)
        {
            var unitPrice =
                basketItem.Product.DiscountPrice.HasValue &&
                basketItem.Product.DiscountPrice.Value > 0 &&
                basketItem.Product.DiscountPrice.Value < basketItem.Product.Price
                    ? basketItem.Product.DiscountPrice.Value
                    : basketItem.Product.Price;

            var itemTotal = unitPrice * basketItem.Quantity;
            totalProductPrice += itemTotal;

            var mainImage = basketItem.Product.Images
                .OrderByDescending(x => x.IsMain)
                .ThenBy(x => x.Order)
                .Select(x => x.ImageUrl)
                .FirstOrDefault();

            var productLink = $"https://nemesisbaku.az/products/{basketItem.ProductId}";

            order.Items.Add(new OrderItem
            {
                ProductId = basketItem.ProductId,
                ProductVariantId = basketItem.ProductVariantId,

                ProductName = basketItem.Product.Name,
                ProductCode = basketItem.Product.ProductCode,

                SizeValue = basketItem.ProductVariant.Size.Value,
                ColorName = basketItem.ProductVariant.Color.Name,

                UnitPrice = unitPrice,
                Quantity = basketItem.Quantity,
                TotalPrice = itemTotal,

                ProductImageUrl = mainImage,
                ProductLink = productLink
            });

            basketItem.ProductVariant.StockCount -= basketItem.Quantity;

            basketItem.IsDeleted = true;
            basketItem.UpdatedAt = DateTime.UtcNow;
        }

        decimal promoDiscountAmount = 0;

        if (!string.IsNullOrWhiteSpace(dto.PromoCode))
        {
            var now = DateTime.UtcNow;
            var code = dto.PromoCode.Trim().ToUpper();

            var promo = await _context.PromoCodes
                .FirstOrDefaultAsync(x =>
                    x.Code == code &&
                    x.IsActive &&
                    x.StartDate <= now &&
                    (!x.EndDate.HasValue || x.EndDate.Value >= now));

            if (promo == null)
                return BadRequest(ApiResponse<string>.Fail("Promo kod yanlışdır və ya aktiv deyil"));

            if (promo.UsageLimit.HasValue && promo.UsedCount >= promo.UsageLimit.Value)
                return BadRequest(ApiResponse<string>.Fail("Promo kod istifadə limitinə çatıb"));

            if (promo.MinOrderAmount.HasValue && totalProductPrice < promo.MinOrderAmount.Value)
                return BadRequest(ApiResponse<string>.Fail(
                    $"Bu promo kod üçün minimum sifariş məbləği {promo.MinOrderAmount.Value} AZN olmalıdır"));

            promoDiscountAmount = promo.DiscountType == DiscountType.Percentage
                ? totalProductPrice * promo.DiscountValue / 100
                : promo.DiscountValue;

            if (promoDiscountAmount > totalProductPrice)
                promoDiscountAmount = totalProductPrice;

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
        order.TotalPrice = totalProductPrice - promoDiscountAmount + order.DeliveryPrice;

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
                Title = string.IsNullOrWhiteSpace(dto.AddressTitle) ? "Ünvan" : dto.AddressTitle,
                AddressText = dto.AddressText,
                Latitude = dto.Latitude.Value,
                Longitude = dto.Longitude.Value,
                BuildingNumber = dto.BuildingNumber,
                Floor = dto.Floor,
                Apartment = dto.Apartment,
                Note = dto.Note,
                IsDefault = false
            };

            _context.UserAddresses.Add(address);
        }

        _context.Orders.Add(order);

        await _telegramOutbox.EnqueueAsync(
            order,
            HttpContext.RequestAborted);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(ApiResponse<Guid>.Ok(order.Id, "Sifariş uğurla yaradıldı"));
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetUserId();

        var orders = await _context.Orders
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
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
            .ToListAsync();

        return Ok(ApiResponse<List<OrderListDto>>.Ok(orders));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderDetail(Guid id)
    {
        var userId = GetUserId();

        var order = await _context.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (order == null)
            return NotFound(ApiResponse<string>.Fail("Sifariş tapılmadı"));

        var dto = new OrderDetailDto
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

            Items = order.Items.Select(x => new OrderItemDto
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
            }).ToList()
        };

        return Ok(ApiResponse<OrderDetailDto>.Ok(dto));
    }

    [HttpPost("calculate-delivery")]
    public async Task<IActionResult> CalculateDelivery(CalculateDeliveryDto dto)
    {
        if (dto.Latitude < -90 || dto.Latitude > 90)
            return BadRequest(ApiResponse<string>.Fail("Latitude düzgün deyil"));

        if (dto.Longitude < -180 || dto.Longitude > 180)
            return BadRequest(ApiResponse<string>.Fail("Longitude düzgün deyil"));

        var storeInfo = await _context.StoreInfos.FirstOrDefaultAsync();

        if (storeInfo == null)
            return BadRequest(ApiResponse<string>.Fail("Mağaza məlumatları tapılmadı"));

        if (!storeInfo.Latitude.HasValue || !storeInfo.Longitude.HasValue)
            return BadRequest(ApiResponse<string>.Fail("Mağaza koordinatları təyin edilməyib"));

        var distanceKm = DeliveryPriceCalculator.CalculateDistanceKm(
            storeInfo.Latitude.Value,
            storeInfo.Longitude.Value,
            dto.Latitude,
            dto.Longitude);

        var deliveryPrice = DeliveryPriceCalculator.CalculateDeliveryPrice(
            distanceKm,
            _deliverySettings);

        var result = new CalculateDeliveryResultDto
        {
            DistanceKm = distanceKm,
            DeliveryPrice = deliveryPrice
        };

        return Ok(ApiResponse<CalculateDeliveryResultDto>.Ok(result));
    }
}
