using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using System.Security.Claims;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminOrdersController : ControllerBase

{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly IWhatsAppService _whatsAppService;

    public AdminOrdersController(
        AppDbContext context,
        IAuditLogService auditLogService,
        IWhatsAppService whatsAppService)
    {
        _context = context;
        _auditLogService = auditLogService;
        _whatsAppService = whatsAppService;
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
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.Orders.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

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

            DeliveryDate = order.DeliveryDate,
            DeliveryTimeRange = order.DeliveryTimeRange,

            DeliveryPrice = order.DeliveryPrice,
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
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound(ApiResponse<string>.Fail("Sifariş tapılmadı"));

        if (order.Status == dto.NewStatus)
            return BadRequest(ApiResponse<string>.Fail("Sifariş artıq bu statusdadır"));

        var oldStatus = order.Status;

        order.Status = dto.NewStatus;
        order.UpdatedAt = DateTime.UtcNow;

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

        await SendOrderStatusWhatsAppMessageAsync(order, dto.NewStatus);


        return Ok(ApiResponse<string>.Ok("Sifariş statusu yeniləndi"));
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

    private async Task SendOrderStatusWhatsAppMessageAsync(Order order, OrderStatus newStatus)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerPhoneNumber))
            return;

        string? message = null;

        if (newStatus == OrderStatus.Confirmed)
        {
            message =
$@"Salam {order.CustomerFullName}

Sifarişiniz qəbul olundu və hazırlanır.

Sifariş nömrəsi:
{order.OrderNumber}

Yekun məbləğ:
{order.TotalPrice} AZN

NemesisBaku";
        }

        if (newStatus == OrderStatus.OnDelivery)
        {
            var estimatedMinutes = Math.Max(
                20,
                (int)Math.Round(((order.DeliveryDistanceKm ?? 0) / 20m) * 60m));

            var productsText = string.Join(", ", order.Items.Select(x => x.ProductName));

            message =
    $@"Salam {order.CustomerFullName}

Sifarişiniz dağıtıma çıxıb.

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

NemesisBaku";
        }

        if (newStatus == OrderStatus.Delivered)
        {
            message =
    $@"Salam {order.CustomerFullName}

Sifarişiniz uğurla təhvil verildi.

NemesisBaku seçdiyiniz üçün təşəkkür edirik.";
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            await _whatsAppService.SendTextMessageAsync(
                order.CustomerPhoneNumber,
                message);
        }
    }
}
