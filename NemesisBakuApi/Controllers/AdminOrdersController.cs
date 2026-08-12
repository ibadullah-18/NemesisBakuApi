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
    private readonly ILogger<AdminOrdersController> _logger;

    public AdminOrdersController(
        AppDbContext context,
        IAuditLogService auditLogService,
        IEmailService emailService,
        ILogger<AdminOrdersController> logger)
    {
        _context = context;
        _auditLogService = auditLogService;
        _emailService = emailService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Orders
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(
                x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();

            query = query.Where(x =>
                x.OrderNumber.Contains(searchValue) ||
                x.CustomerFullName.Contains(searchValue) ||
                x.CustomerPhoneNumber.Contains(searchValue));
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var orders = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminOrderListDto
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                CustomerFullName = x.CustomerFullName,
                CustomerPhoneNumber =
                    x.CustomerPhoneNumber,
                TotalPrice = x.TotalPrice,
                DeliveryType = x.DeliveryType,
                PaymentMethod = x.PaymentMethod,
                Status = x.Status,
                IsWhatsappMessageSent =
                    x.IsWhatsappMessageSent,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<AdminOrderListDto>
        {
            Items = orders,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount / (double)pageSize)
        };

        return Ok(
            ApiResponse<PagedResult<AdminOrderListDto>>
                .Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.User)
            .Include(x => x.Items)
            .Include(x => x.StatusHistories)
                .ThenInclude(x => x.ChangedByUser)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Sifariş tapılmadı"));
        }

        var result = new OrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,

            CustomerFullName = order.CustomerFullName,
            CustomerPhoneNumber =
                order.CustomerPhoneNumber,

            DeliveryType = order.DeliveryType,
            PaymentMethod = order.PaymentMethod,

            AddressText = order.AddressText,
            Latitude = order.Latitude,
            Longitude = order.Longitude,

            BuildingNumber = order.BuildingNumber,
            Floor = order.Floor,
            Apartment = order.Apartment,

            DeliveryDate = order.DeliveryDate,
            DeliveryTimeRange =
                order.DeliveryTimeRange,

            DeliveryPrice = order.DeliveryPrice,
            DeliveryDistanceKm =
                order.DeliveryDistanceKm,

            Note = order.Note,

            TotalProductPrice =
                order.TotalProductPrice,

            PromoDiscountAmount =
                order.PromoDiscountAmount,

            TotalPrice = order.TotalPrice,
            Status = order.Status,

            IsWhatsappMessageSent =
                order.IsWhatsappMessageSent,

            WhatsappMessageSentAt =
                order.WhatsappMessageSentAt,

            CreatedAt = order.CreatedAt,

            Items = order.Items
                .Select(x => new OrderItemDto
                {
                    ProductId = x.ProductId,
                    ProductVariantId =
                        x.ProductVariantId,

                    ProductName = x.ProductName,
                    ProductCode = x.ProductCode,

                    SizeValue = x.SizeValue,
                    ColorName = x.ColorName,

                    UnitPrice = x.UnitPrice,
                    Quantity = x.Quantity,
                    TotalPrice = x.TotalPrice,

                    ProductImageUrl =
                        x.ProductImageUrl,

                    ProductLink = x.ProductLink
                })
                .ToList()
        };

        return Ok(
            ApiResponse<OrderDetailDto>.Ok(result));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateOrderStatusDto dto,
        CancellationToken cancellationToken)
    {
        var adminId = GetUserId();

        if (!Enum.IsDefined(dto.NewStatus))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Sifariş statusu düzgün deyil"));
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        Order? order = null;
        OrderStatus oldStatus = default;
        var stockReturnedNow = false;

        try
        {
            order = await _context.Orders
                .AsSplitQuery()
                .Include(x => x.User)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

            if (order == null)
            {
                return NotFound(
                    ApiResponse<string>.Fail(
                        "Sifariş tapılmadı"));
            }

            if (order.Status == dto.NewStatus)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Sifariş artıq bu statusdadır"));
            }

            if (!OrderStatusRules.CanTransition(
                    order.Status,
                    dto.NewStatus))
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        OrderStatusRules
                            .GetTransitionErrorMessage(
                                order.Status,
                                dto.NewStatus)));
            }

            oldStatus = order.Status;
            var updatedAt = DateTime.UtcNow;

            order.Status = dto.NewStatus;
            order.UpdatedAt = updatedAt;

            var shouldReturnStock =
                OrderStatusRules.RequiresStockReturn(
                    dto.NewStatus) &&
                !order.StockReturned;

            if (shouldReturnStock)
            {
                await ReturnOrderStockAsync(
                    order,
                    updatedAt,
                    cancellationToken);

                stockReturnedNow = true;
            }

            _context.OrderStatusHistories.Add(
                new OrderStatusHistory
                {
                    OrderId = order.Id,
                    OldStatus = oldStatus,
                    NewStatus = dto.NewStatus,
                    ChangedByUserId = adminId,
                    Note = string.IsNullOrWhiteSpace(dto.Note)
                        ? null
                        : dto.Note.Trim()
                });

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _logger.LogWarning(
                exception,
                "Sifariş statusunda concurrency problemi. OrderId: {OrderId}",
                id);

            return Conflict(
                ApiResponse<string>.Fail(
                    "Sifariş və ya stok məlumatı başqa əməliyyat tərəfindən dəyişdirildi. Səhifəni yeniləyib təkrar yoxlayın"));
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }

        await WriteAuditLogSafelyAsync(
            adminId,
            "UpdateStatus",
            "Order",
            order.Id.ToString(),
            $"Sifariş statusu dəyişdirildi: " +
            $"{oldStatus} → {order.Status}. " +
            $"OrderNumber: {order.OrderNumber}");

        if (stockReturnedNow)
        {
            await WriteAuditLogSafelyAsync(
                adminId,
                "StockReturned",
                "Order",
                order.Id.ToString(),
                "Sifariş ləğv/rədd edildi və məhsullar " +
                "stoka geri qaytarıldı. " +
                $"OrderNumber: {order.OrderNumber}");
        }

        await SendStatusEmailSafelyAsync(order);

        return Ok(
            ApiResponse<string>.Ok(
                "Sifariş statusu yeniləndi"));
    }

    [HttpGet("{id:guid}/status-whatsapp-link")]
    public async Task<IActionResult> GetStatusWhatsAppLink(
        Guid id,
        [FromQuery] OrderStatus status,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Sifariş statusu düzgün deyil"));
        }

        var order = await _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Sifariş tapılmadı"));
        }

        if (string.IsNullOrWhiteSpace(
                order.CustomerPhoneNumber))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Müştərinin telefon nömrəsi yoxdur"));
        }

        var message = BuildOrderStatusMessage(
            order,
            status);

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu status üçün WhatsApp mesajı yoxdur"));
        }

        var phone = NormalizePhone(
            order.CustomerPhoneNumber);

        var encodedMessage =
            Uri.EscapeDataString(message);

        var url =
            $"https://wa.me/{phone}?text={encodedMessage}";

        var result = new WhatsAppManualLinkDto
        {
            Url = url,
            Message = message
        };

        return Ok(
            ApiResponse<WhatsAppManualLinkDto>
                .Ok(result));
    }

    [HttpGet("{id:guid}/courier-whatsapp-link")]
    public async Task<IActionResult> GetCourierWhatsAppLink(
        Guid id,
        [FromQuery] string courierPhoneNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                courierPhoneNumber))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Kuryer nömrəsi daxil edilməlidir"));
        }

        var order = await _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Sifariş tapılmadı"));
        }

        var message = BuildCourierMessage(order);
        var phone = NormalizePhone(
            courierPhoneNumber);

        var encodedMessage =
            Uri.EscapeDataString(message);

        var url =
            $"https://wa.me/{phone}?text={encodedMessage}";

        var result = new WhatsAppManualLinkDto
        {
            Url = url,
            Message = message
        };

        return Ok(
            ApiResponse<WhatsAppManualLinkDto>
                .Ok(result));
    }

    private async Task ReturnOrderStockAsync(
        Order order,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var quantitiesByVariant = order.Items
            .GroupBy(x => x.ProductVariantId)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(item => item.Quantity));

        var variantIds = quantitiesByVariant.Keys.ToList();

        var variants = await _context.ProductVariants
            .Where(x => variantIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                cancellationToken);

        if (variants.Count != variantIds.Count)
        {
            throw new InvalidOperationException(
                "Sifarişdəki məhsul variantlarından biri tapılmadı");
        }

        foreach (var variantData in quantitiesByVariant)
        {
            var variant = variants[variantData.Key];

            variant.StockCount += variantData.Value;
            variant.UpdatedAt = updatedAt;
        }

        order.StockReturned = true;
        order.StockReturnedAt = updatedAt;
    }

    private async Task WriteAuditLogSafelyAsync(
        Guid adminId,
        string action,
        string entityName,
        string? entityId,
        string? description)
    {
        try
        {
            await _auditLogService.CreateAsync(
                adminId,
                action,
                entityName,
                entityId,
                description,
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString(),
                Request.Headers.UserAgent.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Audit log yaradıla bilmədi. Action: {Action}, EntityId: {EntityId}",
                action,
                entityId);
        }
    }

    private async Task SendStatusEmailSafelyAsync(
        Order order)
    {
        if (order.User == null ||
            string.IsNullOrWhiteSpace(order.User.Email))
        {
            return;
        }

        try
        {
            await _emailService.SendOrderStatusAsync(
                order.User.Email,
                order.CustomerFullName,
                order.OrderNumber,
                order.Status,
                order.TotalPrice);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Sifariş status emaili göndərilə bilmədi. OrderId: {OrderId}",
                order.Id);
        }
    }

    private static string BuildOrderStatusMessage(
        Order order,
        OrderStatus status)
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
                (int)Math.Round(
                    ((order.DeliveryDistanceKm ?? 0) /
                     20m) * 60m));

            var productsText = string.Join(
                ", ",
                order.Items.Select(
                    x => x.ProductName));

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

        return string.Empty;
    }

    private static string BuildCourierMessage(
        Order order)
    {
        var mapLink =
            order.Latitude.HasValue &&
            order.Longitude.HasValue
                ? "https://www.google.com/maps?q=" +
                  $"{order.Latitude}," +
                  $"{order.Longitude}"
                : "Konum yoxdur";

        var productsText = string.Join(
            "\n",
            order.Items.Select(x =>
                $"- {x.ProductName} | " +
                $"{x.SizeValue} | " +
                $"{x.ColorName} | " +
                $"{x.Quantity} ədəd"));

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

    private static string NormalizePhone(
        string phone)
    {
        return phone
            .Replace("+", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("-", "");
    }
}