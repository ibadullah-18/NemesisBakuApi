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
using System.Text;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly DeliverySettings _deliverySettings;

    public OrdersController(
        AppDbContext context,
        IWhatsAppService whatsAppService,
        IOptions<DeliverySettings> deliveryOptions)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _deliverySettings = deliveryOptions.Value;
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
                basketItem.Product.IsDiscounted && basketItem.Product.DiscountPrice.HasValue
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

            if (basketItem.ProductVariant.StockCount > 0 &&
                            basketItem.ProductVariant.StockCount <= 2)
            {
                await _whatsAppService.SendLowStockNotificationAsync(
                    storeInfo.WhatsAppNumber,
                    basketItem.Product.Name,
                    basketItem.ProductVariant.Size.Value,
                    basketItem.ProductVariant.Color.Name,
                    basketItem.ProductVariant.StockCount);
            }

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

        _context.Orders.Add(order);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var message = BuildOrderWhatsAppMessage(order);

        var sent = await _whatsAppService.SendTextMessageAsync(
            storeInfo.WhatsAppNumber,
            message);

        order.IsWhatsappMessageSent = sent;
        order.WhatsappMessageSentAt = sent ? DateTime.UtcNow : null;

        await _context.SaveChangesAsync();

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

    private string BuildOrderWhatsAppMessage(Order order)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Yeni sifariş var");
        sb.AppendLine($"Sifariş nömrəsi: {order.OrderNumber}");
        sb.AppendLine($"Müştəri: {order.CustomerFullName}");
        sb.AppendLine($"Telefon: {order.CustomerPhoneNumber}");
        sb.AppendLine();

        sb.AppendLine("Məhsullar:");

        foreach (var item in order.Items)
        {
            sb.AppendLine($"- {item.ProductName}");
            sb.AppendLine($"  Kod: {item.ProductCode}");
            sb.AppendLine($"  Razmer: {item.SizeValue}");
            sb.AppendLine($"  Rəng: {item.ColorName}");
            sb.AppendLine($"  Say: {item.Quantity}");
            sb.AppendLine($"  Qiymət: {item.UnitPrice} AZN");
            sb.AppendLine($"  Link: {item.ProductLink}");
            sb.AppendLine();
        }

        sb.AppendLine($"Məhsulların cəmi: {order.TotalProductPrice} AZN");

        if (order.PromoDiscountAmount > 0)
        {
            sb.AppendLine($"Promo endirim: -{order.PromoDiscountAmount} AZN");
        }

        sb.AppendLine($"Çatdırılma: {order.DeliveryPrice} AZN");
        sb.AppendLine($"Yekun: {order.TotalPrice} AZN");
        sb.AppendLine();

        sb.AppendLine($"Çatdırılma tipi: {order.DeliveryType}");
        sb.AppendLine($"Ödəniş: {order.PaymentMethod}");

        if (order.DeliveryType == DeliveryType.HomeDelivery)
        {
            sb.AppendLine($"Ünvan: {order.AddressText}");
            sb.AppendLine($"Məsafə: {order.DeliveryDistanceKm} km");
            sb.AppendLine($"Bina/Blok: {order.BuildingNumber}");
            sb.AppendLine($"Mərtəbə: {order.Floor}");
            sb.AppendLine($"Mənzil: {order.Apartment}");
            sb.AppendLine($"Tarix: {order.DeliveryDate:dd.MM.yyyy}");
            sb.AppendLine($"Saat: {order.DeliveryTimeRange}");

            if (order.Latitude.HasValue && order.Longitude.HasValue)
            {
                sb.AppendLine($"Konum: https://www.google.com/maps?q={order.Latitude},{order.Longitude}");
            }
        }
        else
        {
            sb.AppendLine("Təhvil alma: Mağazadan götürüləcək");
        }

        if (!string.IsNullOrWhiteSpace(order.Note))
        {
            sb.AppendLine();
            sb.AppendLine($"Qeyd: {order.Note}");
        }

        return sb.ToString();
    }
}