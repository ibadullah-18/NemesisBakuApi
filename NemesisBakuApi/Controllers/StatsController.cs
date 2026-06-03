using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Stats;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StatsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid? GetUserIdOrNull()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return Guid.Parse(userId);
    }

    [HttpPost("track-visit")]
    public async Task<IActionResult> TrackVisit(TrackVisitDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.VisitorId))
            return BadRequest(ApiResponse<string>.Fail("VisitorId boş ola bilməz"));

        var visit = new SiteVisit
        {
            UserId = GetUserIdOrNull(),
            VisitorId = dto.VisitorId,
            PageUrl = dto.PageUrl,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            VisitedAt = DateTime.UtcNow
        };

        _context.SiteVisits.Add(visit);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Visit qeydə alındı"));
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var totalUsers = await _context.Users
            .CountAsync(x => !x.IsDeleted);

        var activeUsers = await _context.Users
            .CountAsync(x => !x.IsDeleted && x.IsActive);

        var totalOrders = await _context.Orders.CountAsync();

        var pendingOrders = await _context.Orders
            .CountAsync(x => x.Status == OrderStatus.Pending);

        var confirmedOrders = await _context.Orders
            .CountAsync(x => x.Status == OrderStatus.Confirmed);

        var onDeliveryOrders = await _context.Orders
            .CountAsync(x => x.Status == OrderStatus.OnDelivery);

        var deliveredOrders = await _context.Orders
            .CountAsync(x => x.Status == OrderStatus.Delivered);

        var cancelledOrders = await _context.Orders
            .CountAsync(x =>
                x.Status == OrderStatus.Cancelled ||
                x.Status == OrderStatus.Rejected);

        var totalProducts = await _context.Products.CountAsync();

        var activeProducts = await _context.Products
            .CountAsync(x => x.IsActive);

        var lowStockProducts = await _context.ProductVariants
            .Where(x => x.IsActive && x.StockCount > 0 && x.StockCount <= 2)
            .Select(x => x.ProductId)
            .Distinct()
            .CountAsync();

        var totalRevenue = await _context.Orders
            .Where(x => x.Status == OrderStatus.Delivered)
            .SumAsync(x => x.TotalPrice);

        var totalPageViews = await _context.SiteVisits.CountAsync();

        var uniqueVisitors = await _context.SiteVisits
            .Select(x => x.VisitorId)
            .Distinct()
            .CountAsync();

        var whatsappProductClicks = await _context.WhatsAppClickLogs
            .CountAsync(x => x.ClickType == "ProductInquiry");

        var whatsappBasketClicks = await _context.WhatsAppClickLogs
            .CountAsync(x => x.ClickType == "BasketInquiry");

        var totalWhatsappClicks = await _context.WhatsAppClickLogs.CountAsync();

        var dto = new DashboardStatsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,

            TotalOrders = totalOrders,
            PendingOrders = pendingOrders,
            ConfirmedOrders = confirmedOrders,
            OnDeliveryOrders = onDeliveryOrders,
            DeliveredOrders = deliveredOrders,
            CancelledOrders = cancelledOrders,

            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            LowStockProducts = lowStockProducts,

            TotalRevenue = totalRevenue,

            TotalPageViews = totalPageViews,
            UniqueVisitors = uniqueVisitors,

            WhatsAppProductClicks = whatsappProductClicks,
            WhatsAppBasketClicks = whatsappBasketClicks,
            TotalWhatsAppClicks = totalWhatsappClicks
        };

        return Ok(ApiResponse<DashboardStatsDto>.Ok(dto));
    }
}