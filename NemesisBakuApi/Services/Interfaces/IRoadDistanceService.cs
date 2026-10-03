namespace NemesisBakuApi.Services.Interfaces;

public record RoadPoint(decimal Latitude, decimal Longitude);
public interface IRoadDistanceService
{
    Task<IReadOnlyList<decimal?>> GetDistancesKmAsync(IReadOnlyList<RoadPoint> origins, RoadPoint destination, CancellationToken ct);
}
public sealed class RoadRoutingException : Exception
{
    public RoadRoutingException(string message) : base(message) { }
    public RoadRoutingException(string message, Exception inner) : base(message, inner) { }
}
