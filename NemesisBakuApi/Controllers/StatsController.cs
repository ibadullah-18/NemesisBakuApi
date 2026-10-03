using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Settings;
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
    private readonly DatabaseCleanupSettings _cleanup;

    public StatsController(AppDbContext context, IOptions<DatabaseCleanupSettings>? cleanup = null)
    {
        _context = context;
        _cleanup = cleanup?.Value ?? new DatabaseCleanupSettings();
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
        // Old clients reported only home visits: never mix those with page views.
        if (string.IsNullOrWhiteSpace(dto.EventId))
            return Ok(ApiResponse<string>.Ok("Köhnə ziyarət formatı nəzərə alınmadı"));
        if (string.IsNullOrWhiteSpace(dto.VisitorId) || string.IsNullOrWhiteSpace(dto.SessionId) ||
            dto.VisitorId.Length > 128 || dto.EventId.Length > 128 || dto.SessionId.Length > 128)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "VisitorId boş ola bilməz"));
        }

        var visitorId = LimitLength(
            dto.VisitorId.Trim(),
            128)!;

        var pageUrl = dto.PageUrl?.Trim().Split('?', '#')[0];
        if (string.IsNullOrEmpty(pageUrl) || !pageUrl.StartsWith('/') || pageUrl.StartsWith("//") || pageUrl.Length > 500)
            return BadRequest(ApiResponse<string>.Fail("Səhifə yolu düzgün deyil"));
        var firstSegment = pageUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        var agent = Request.Headers.UserAgent.ToString();
        if (firstSegment is "admin" or "superadmin" or "api" or "tests" ||
            User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
            System.Text.RegularExpressions.Regex.IsMatch(agent, "bot|crawler|spider|headless|lighthouse|pagespeed|selenium|playwright", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return Ok(ApiResponse<string>.Ok("Daxili və avtomatik baxış nəzərə alınmadı"));

        var recentlyTracked = await _context.SiteVisits
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.EventId == dto.EventId,
                cancellationToken);

        if (recentlyTracked)
        {
            return Ok(
                ApiResponse<string>.Ok(
                    "Visit artıq qeydə alınıb"));
        }

        var visit = new SiteVisit
        {
            UserId = GetUserIdOrNull(),
            VisitorId = visitorId,
            EventId = dto.EventId,
            SessionId = dto.SessionId,
            PageUrl = pageUrl,

            IpAddress = LimitLength(
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString(),
                64),

            UserAgent = LimitLength(
                Request.Headers.UserAgent.ToString(),
                512),

            VisitedAt = DateTime.UtcNow
        };

        _context.SiteVisits.Add(visit);

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            _context.Entry(visit).State = EntityState.Detached;
            return Ok(ApiResponse<string>.Ok("Baxış artıq qeydə alınıb"));
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Visit qeydə alındı"));
    }

    private Task<DateTime?> GetTrafficStartAsync(CancellationToken ct) =>
        _context.TrafficStatisticsPeriods.AsNoTracking()
            .Select(x => (DateTime?)x.StartsAtUtc).MaxAsync(ct);

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("traffic/restart")]
    public async Task<IActionResult> RestartTrafficStatistics(CancellationToken cancellationToken)
    {
        var userId = GetUserIdOrNull();
        if (!userId.HasValue) return Unauthorized();
        var period = new TrafficStatisticsPeriod { StartsAtUtc = DateTime.UtcNow, StartedByUserId = userId.Value };
        _context.TrafficStatisticsPeriods.Add(period);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<DateTime>.Ok(period.StartsAtUtc, "Ziyarət statistikası yeni tarixdən başladıldı."));
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats(
        CancellationToken cancellationToken, [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null)
    {
        if (from.HasValue && to.HasValue && from >= to)
            return BadRequest(ApiResponse<string>.Fail("Tarix aralığı düzgün deyil"));
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

        var trafficStart = await GetTrafficStartAsync(cancellationToken);
        var trafficFrom = from?.UtcDateTime;
        if (trafficStart.HasValue && (!trafficFrom.HasValue || trafficFrom < trafficStart)) trafficFrom = trafficStart;
        var trafficTo = to?.UtcDateTime;
        var visitStats = await TrafficStatisticsQuery.ReadAsync(_context.SiteVisits, trafficFrom, trafficTo, cancellationToken);

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
            VisitSessions = visitStats?.Sessions ?? 0,
            TrafficFromUtc = trafficFrom.HasValue ? DateTime.SpecifyKind(trafficFrom.Value, DateTimeKind.Utc) : null,
            TrafficToUtc = trafficTo.HasValue ? DateTime.SpecifyKind(trafficTo.Value, DateTimeKind.Utc) : null,
            TrafficStatisticsStartsAtUtc = trafficStart.HasValue
                ? DateTime.SpecifyKind(trafficStart.Value, DateTimeKind.Utc) : null,
            TrafficRetentionDays = _cleanup.Enabled ? Math.Max(1, _cleanup.SiteVisitRetentionDays) : null,

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

    private static string? LimitLength(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }
}
