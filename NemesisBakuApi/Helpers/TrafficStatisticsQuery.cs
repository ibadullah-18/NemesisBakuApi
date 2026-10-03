using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Entities;

namespace NemesisBakuApi.Helpers;

public record TrafficCounts(int Total, int Unique, int Sessions);
public static class TrafficStatisticsQuery
{
    public static async Task<TrafficCounts> ReadAsync(IQueryable<SiteVisit> visits, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var range = visits.AsNoTracking().Where(x => x.EventId != null &&
            (!from.HasValue || x.VisitedAt >= from.Value) && (!to.HasValue || x.VisitedAt < to.Value));
        var total = await range.CountAsync(ct);
        var unique = await range.Select(x => x.VisitorId).Distinct().CountAsync(ct);
        var sessions = await range.Select(x => new { x.VisitorId, x.SessionId }).Distinct().CountAsync(ct);
        return new(total, unique, sessions);
    }
}
