using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Controllers;
using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using Xunit;

namespace NemesisBakuApi.Tests;

public class TrafficStatisticsTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly AppDbContext db;
    private readonly StatsController controller;
    private readonly Guid adminId = Guid.NewGuid();
    public TrafficStatisticsTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        controller = new(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    }

    [Fact]
    public async Task Repeated_visit_is_deduplicated_and_other_browser_counts_separately()
    {
        await controller.TrackVisit(new() { VisitorId = "browser-a", PageUrl = "/", EventId = "event-a", SessionId = "session-a" }, default);
        await controller.TrackVisit(new() { VisitorId = "browser-a", PageUrl = "/", EventId = "event-a", SessionId = "session-a" }, default);
        await controller.TrackVisit(new() { VisitorId = "browser-b", PageUrl = "/", EventId = "event-b", SessionId = "session-b" }, default);
        Assert.Equal(2, await db.SiteVisits.CountAsync());
    }

    [Fact]
    public async Task Restart_preserves_history_and_allows_same_browser_in_new_period()
    {
        await controller.TrackVisit(new() { VisitorId = "browser-a", PageUrl = "/", EventId = "event-a", SessionId = "session-a" }, default);
        db.WhatsAppClickLogs.Add(new() { ClickType = "ProductInquiry" });
        await db.SaveChangesAsync();
        controller.User.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, adminId.ToString())]));
        Assert.IsType<OkObjectResult>(await controller.RestartTrafficStatistics(default));
        var period = await db.TrafficStatisticsPeriods.SingleAsync();
        Assert.Equal(adminId, period.StartedByUserId);
        Assert.Equal(1, await db.SiteVisits.CountAsync());
        Assert.Equal(0, await db.SiteVisits.CountAsync(x => x.VisitedAt >= period.StartsAtUtc));
        controller.HttpContext.User = new ClaimsPrincipal();
        // A new page opening uses a new event even when the visitor/session stay the same.
        await controller.TrackVisit(new() { VisitorId = "browser-a", PageUrl = "/", EventId = "event-after-restart", SessionId = "session-a" }, default);
        Assert.Equal(2, await db.SiteVisits.CountAsync());
        Assert.Equal(1, await db.SiteVisits.CountAsync(x => x.VisitedAt >= period.StartsAtUtc));
        Assert.Equal(1, await db.WhatsAppClickLogs.CountAsync());
    }

    [Fact]
    public async Task Restart_requires_identity_and_superadmin_role()
    {
        Assert.IsType<UnauthorizedResult>(await controller.RestartTrafficStatistics(default));
        Assert.Empty(await db.TrafficStatisticsPeriods.ToListAsync());
        var attribute = (AuthorizeAttribute)Attribute.GetCustomAttribute(typeof(StatsController)
            .GetMethod(nameof(StatsController.RestartTrafficStatistics))!, typeof(AuthorizeAttribute))!;
        Assert.Equal("SuperAdmin", attribute.Roles);
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }

    [Fact]
    public async Task Five_pages_are_five_views_one_visitor_one_session_and_retry_is_ignored()
    {
        for (var i = 0; i < 5; i++)
            await controller.TrackVisit(new() { VisitorId = "v", SessionId = "s", EventId = $"e{i}", PageUrl = $"/products/{i}" }, default);
        await controller.TrackVisit(new() { VisitorId = "v", SessionId = "s", EventId = "e0", PageUrl = "/products/0" }, default);
        Assert.Equal(new TrafficCounts(5, 1, 1), await TrafficStatisticsQuery.ReadAsync(db.SiteVisits, null, null, default));
        await controller.TrackVisit(new() { VisitorId = "v", SessionId = "next-session", EventId = "new", PageUrl = "/" }, default);
        Assert.Equal(new TrafficCounts(6, 1, 2), await TrafficStatisticsQuery.ReadAsync(db.SiteVisits, null, null, default));
    }

    [Theory]
    [InlineData("/Admin/orders", "Mozilla/5.0")]
    [InlineData("/SuperAdmin", "Mozilla/5.0")]
    [InlineData("/", "Googlebot")]
    public async Task Internal_and_known_bot_views_are_excluded(string path, string userAgent)
    {
        controller.Request.Headers.UserAgent = userAgent;
        await controller.TrackVisit(new() { VisitorId = "v", SessionId = "s", EventId = "e", PageUrl = path }, default);
        Assert.Empty(await db.SiteVisits.ToListAsync());
    }

    [Fact]
    public async Task Date_range_is_inclusive_start_exclusive_end_and_legacy_visits_are_not_mixed()
    {
        var start = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        db.SiteVisits.AddRange(
            new() { VisitorId = "v", SessionId = "s", EventId = "before", VisitedAt = start.AddTicks(-1) },
            new() { VisitorId = "v", SessionId = "s", EventId = "start", VisitedAt = start },
            new() { VisitorId = "v", SessionId = "s2", EventId = "end", VisitedAt = start.AddDays(1) },
            new() { VisitorId = "legacy", VisitedAt = start });
        await db.SaveChangesAsync();
        Assert.Equal(new TrafficCounts(1, 1, 1), await TrafficStatisticsQuery.ReadAsync(db.SiteVisits, start, start.AddDays(1), default));
    }
}
