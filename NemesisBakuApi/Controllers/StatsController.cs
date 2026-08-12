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
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            return null;
        }

        return Guid.TryParse(
            userIdValue,
            out var userId)
                ? userId
                : null;
    }

    [HttpPost("track-visit")]
    public async Task<IActionResult> TrackVisit(
        TrackVisitDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.VisitorId))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "VisitorId boş ola bilməz"));
        }

        var visit = new SiteVisit
        {
            UserId = GetUserIdOrNull(),
            VisitorId = dto.VisitorId,
            PageUrl = dto.PageUrl,

            IpAddress = HttpContext
                .Connection
                .RemoteIpAddress?
                .ToString(),

            UserAgent = Request
                .Headers
                .UserAgent
                .ToString(),

            VisitedAt = DateTime.UtcNow
        };

        _context.SiteVisits.Add(visit);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Visit qeydə alındı"));
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats(
        CancellationToken cancellationToken)
    {
        var userStats = await _context.Users
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => 1)
            .Select(group => new
            {
                Total = group.Count(),

                Active = group.Count(
                    x => x.IsActive)
            })
            .FirstOrDefaultAsync(
                cancellationToken);

        var orderStats = await _context.Orders
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(group => new
            {
                Total = group.Count(),

                Pending = group.Count(
                    x => x.Status ==
                         OrderStatus.Pending),

                Confirmed = group.Count(
                    x => x.Status ==
                         OrderStatus.Confirmed),

                OnDelivery = group.Count(
                    x => x.Status ==
                         OrderStatus.OnDelivery),

                Delivered = group.Count(
                    x => x.Status ==
                         OrderStatus.Delivered),

                Cancelled = group.Count(
                    x =>
                        x.Status ==
                        OrderStatus.Cancelled ||
                        x.Status ==
                        OrderStatus.Rejected),

                Revenue = group.Sum(
                    x =>
                        x.Status ==
                        OrderStatus.Delivered
                            ? x.TotalPrice
                            : 0m)
            })
            .FirstOrDefaultAsync(
                cancellationToken);

        var productStats = await _context.Products
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(group => new
            {
                Total = group.Count(),

                Active = group.Count(
                    x => x.IsActive)
            })
            .FirstOrDefaultAsync(
                cancellationToken);

        var lowStockProducts =
            await _context.ProductVariants
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.StockCount > 0 &&
                    x.StockCount <= 2)
                .Select(x => x.ProductId)
                .Distinct()
                .CountAsync(cancellationToken);

        var visitStats = await _context.SiteVisits
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(group => new
            {
                Total = group.Count(),

                Unique = group
                    .Select(x => x.VisitorId)
                    .Distinct()
                    .Count()
            })
            .FirstOrDefaultAsync(
                cancellationToken);

        var whatsappStats =
            await _context.WhatsAppClickLogs
                .AsNoTracking()
                .GroupBy(x => 1)
                .Select(group => new
                {
                    Total = group.Count(),

                    ProductClicks = group.Count(
                        x => x.ClickType ==
                             "ProductInquiry"),

                    BasketClicks = group.Count(
                        x => x.ClickType ==
                             "BasketInquiry")
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        var dto = new DashboardStatsDto
        {
            TotalUsers = userStats?.Total ?? 0,
            ActiveUsers = userStats?.Active ?? 0,

            TotalOrders = orderStats?.Total ?? 0,
            PendingOrders =
                orderStats?.Pending ?? 0,
            ConfirmedOrders =
                orderStats?.Confirmed ?? 0,
            OnDeliveryOrders =
                orderStats?.OnDelivery ?? 0,
            DeliveredOrders =
                orderStats?.Delivered ?? 0,
            CancelledOrders =
                orderStats?.Cancelled ?? 0,

            TotalProducts =
                productStats?.Total ?? 0,
            ActiveProducts =
                productStats?.Active ?? 0,
            LowStockProducts =
                lowStockProducts,

            TotalRevenue =
                orderStats?.Revenue ?? 0m,

            TotalPageViews =
                visitStats?.Total ?? 0,
            UniqueVisitors =
                visitStats?.Unique ?? 0,

            WhatsAppProductClicks =
                whatsappStats?.ProductClicks ?? 0,
            WhatsAppBasketClicks =
                whatsappStats?.BasketClicks ?? 0,
            TotalWhatsAppClicks =
                whatsappStats?.Total ?? 0
        };

        return Ok(
            ApiResponse<DashboardStatsDto>.Ok(dto));
    }
}