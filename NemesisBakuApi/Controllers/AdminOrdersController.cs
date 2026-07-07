using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly IEmailService _emailService;

    public AdminOrdersController(
        AppDbContext context,
        IAuditLogService auditLogService,
        IEmailService emailService)
    {
        _context = context;
        _auditLogService = auditLogService;
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
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Orders.AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();

            query = query.Where(x =>
                x.OrderNumber.ToLower().Contains(s) ||
                x.CustomerFullName.ToLower().Contains(s) ||
                x.CustomerPhoneNumber.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminOrderListDto
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                CustomerFullName = x.CustomerFullName,
                CustomerPhoneNumber = x.CustomerPhoneNumber,
                TotalPrice = x.TotalPrice,
                DeliveryType = x.DeliveryType,
                PaymentMethod = x.PaymentMethod,
                Status = x.Status,
                IsWhatsappMessageSent = x.IsWhatsappMessageSent,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var result = new PagedResult<AdminOrderListDto>
        {
            Items = orders,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        return Ok(ApiResponse<PagedResult<AdminOrderListDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderDetail(Guid id)
    {
        var order = await _context.Orders
            .Include(x => x.User)
            .Include(x => x.Items)
            .Include(x => x.StatusHistories)
                .ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(x => x.Id == id);

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

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusDto dto)
    {
        var adminId = GetUserId();

        var order = await _context.Orders
            .Include(x => x.User)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound(ApiResponse<string>.Fail("Sifariş tapılmadı"));

        if (order.Status == dto.NewStatus)
            return BadRequest(ApiResponse<string>.Fail("Sifariş artıq bu statusdadır"));

        var oldStatus = order.Status;

        order.Status = dto.NewStatus;
        order.UpdatedAt = DateTime.UtcNow;

        if ((dto.NewStatus == OrderStatus.Cancelled ||
         dto.NewStatus == OrderStatus.Rejected) &&
        !order.StockReturned)
            {
                foreach (var item in order.Items)
                {
                    var variant = await _context.ProductVariants
                        .FirstOrDefaultAsync(x => x.Id == item.ProductVariantId);

                    if (variant != null)
                    {
                        variant.StockCount += item.Quantity;
                        variant.UpdatedAt = DateTime.UtcNow;
                    }
                }

                order.StockReturned = true;
                order.StockReturnedAt = DateTime.UtcNow;
            }

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            OldStatus = oldStatus,
            NewStatus = dto.NewStatus,
            ChangedByUserId = adminId,
            Note = dto.Note
        });

        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(
            "UpdateStatus",
            "Order",
            order.Id.ToString(),
            $"Sifariş statusu dəyişdirildi: {oldStatus} → {dto.NewStatus}. OrderNumber: {order.OrderNumber}");

        if (order.StockReturned &&
            (dto.NewStatus == OrderStatus.Cancelled ||
             dto.NewStatus == OrderStatus.Rejected))
        {
            await WriteAuditLogAsync(
                "StockReturned",
                "Order",
                order.Id.ToString(),
                $"Sifariş ləğv/rədd edildi və məhsullar stoka geri qaytarıldı. OrderNumber: {order.OrderNumber}");
        }

        if (order.User != null && !string.IsNullOrWhiteSpace(order.User.Email))
        {
            await _emailService.SendOrderStatusAsync(
                order.User.Email,
                order.CustomerFullName,
                order.OrderNumber,
                order.Status,
                order.TotalPrice);
        }

        return Ok(ApiResponse<string>.Ok("Sifariş statusu yeniləndi"));
    }

    [HttpGet("{id}/status-whatsapp-link")]
    public async Task<IActionResult> GetStatusWhatsAppLink(Guid id, [FromQuery] OrderStatus status)
    {
        var order = await _context.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound(ApiResponse<string>.Fail("Sifariş tapılmadı"));

        if (string.IsNullOrWhiteSpace(order.CustomerPhoneNumber))
            return BadRequest(ApiResponse<string>.Fail("Müştərinin telefon nömrəsi yoxdur"));

        var message = BuildOrderStatusMessage(order, status);

        if (string.IsNullOrWhiteSpace(message))
            return BadRequest(ApiResponse<string>.Fail("Bu status üçün WhatsApp mesajı yoxdur"));

        var phone = NormalizePhone(order.CustomerPhoneNumber);
        var encodedMessage = Uri.EscapeDataString(message);
        var url = $"https://wa.me/{phone}?text={encodedMessage}";

        return Ok(ApiResponse<WhatsAppManualLinkDto>.Ok(new WhatsAppManualLinkDto
        {
            Url = url,
            Message = message
        }));
    }

    [HttpGet("{id}/courier-whatsapp-link")]
    public async Task<IActionResult> GetCourierWhatsAppLink(Guid id, [FromQuery] string courierPhoneNumber)
    {
        var order = await _context.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound(ApiResponse<string>.Fail("Sifariş tapılmadı"));

        if (string.IsNullOrWhiteSpace(courierPhoneNumber))
            return BadRequest(ApiResponse<string>.Fail("Kuryer nömrəsi daxil edilməlidir"));

        var message = BuildCourierMessage(order);
        var phone = NormalizePhone(courierPhoneNumber);
        var encodedMessage = Uri.EscapeDataString(message);
        var url = $"https://wa.me/{phone}?text={encodedMessage}";

        return Ok(ApiResponse<WhatsAppManualLinkDto>.Ok(new WhatsAppManualLinkDto
        {
            Url = url,
            Message = message
        }));
    }

    private async Task WriteAuditLogAsync(
        string action,
        string entityName,
        string? entityId,
        string? description)
    {
        await _auditLogService.CreateAsync(
            GetUserId(),
            action,
            entityName,
            entityId,
            description,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }

    private static string BuildOrderStatusMessage(Order order, OrderStatus status)
    {
        if (status == OrderStatus.Confirmed)
        {
            return
$@"Salam {order.CustomerFullName}

Sifarişiniz qəbul olundu.

Sifariş nömrəsi:
{order.OrderNumber}

Yekun məbləğ:
{order.TotalPrice} AZN

nemesisbaku";
        }

        if (status == OrderStatus.Preparing)
        {
            return
$@"Salam {order.CustomerFullName}

Sifarişiniz hazırlanır.

Sifariş nömrəsi:
{order.OrderNumber}

Yekun məbləğ:
{order.TotalPrice} AZN

nemesisbaku";
        }

        if (status == OrderStatus.OnDelivery)
        {
            var estimatedMinutes = Math.Max(
                20,
                (int)Math.Round(((order.DeliveryDistanceKm ?? 0) / 20m) * 60m));

            var productsText = string.Join(", ", order.Items.Select(x => x.ProductName));

            return
$@"Salam {order.CustomerFullName}

Sifarişiniz çatdırılmaya çıxdı.

Sifariş nömrəsi:
{order.OrderNumber}

Məhsullar:
{productsText}

Yekun məbləğ:
{order.TotalPrice} AZN

Çatdırılma:
{order.DeliveryPrice} AZN

Məsafə:
{order.DeliveryDistanceKm} km

Təxmini çatdırılma:
{estimatedMinutes} dəqiqə

nemesisbaku";
        }

        if (status == OrderStatus.Delivered)
        {
            return
$@"Salam {order.CustomerFullName}

Sifarişiniz uğurla təhvil verildi.

nemesisbaku seçdiyiniz üçün təşəkkür edirik.";
        }

        if (status == OrderStatus.Cancelled)
        {
            return
$@"Salam {order.CustomerFullName}

Sifarişiniz ləğv edildi.

Sifariş nömrəsi:
{order.OrderNumber}

Əlavə məlumat üçün bizimlə əlaqə saxlaya bilərsiniz.

nemesisbaku";
        }

        if (status == OrderStatus.Rejected)
        {
            return
$@"Salam {order.CustomerFullName}

Sifarişiniz rədd edildi.

Sifariş nömrəsi:
{order.OrderNumber}

Əlavə məlumat üçün bizimlə əlaqə saxlaya bilərsiniz.

nemesisbaku";
        }

        return "";
    }

    private static string BuildCourierMessage(Order order)
    {
        var mapLink = order.Latitude.HasValue && order.Longitude.HasValue
            ? $"https://www.google.com/maps?q={order.Latitude},{order.Longitude}"
            : "Konum yoxdur";

        var productsText = string.Join("\n", order.Items.Select(x =>
            $"- {x.ProductName} | {x.SizeValue} | {x.ColorName} | {x.Quantity} ədəd"));

        return
$@"Yeni çatdırılma

Sifariş nömrəsi:
{order.OrderNumber}

Müştəri:
{order.CustomerFullName}

Telefon:
{order.CustomerPhoneNumber}

Ünvan:
{order.AddressText}

Bina/Blok:
{order.BuildingNumber}

Mərtəbə:
{order.Floor}

Mənzil:
{order.Apartment}

Qeyd:
{order.Note}

Məhsullar:
{productsText}

Məhsulların cəmi:
{order.TotalProductPrice} AZN

Çatdırılma:
{order.DeliveryPrice} AZN

Yekun alınacaq məbləğ:
{order.TotalPrice} AZN

Məsafə:
{order.DeliveryDistanceKm} km

Xəritə:
{mapLink}

nemesisbaku";
    }

    private static string NormalizePhone(string phone)
    {
        return phone
            .Replace("+", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("-", "");
    }
}