namespace NemesisBakuApi.Entities;

// Append-only history: restarting statistics never deletes visits.
public class TrafficStatisticsPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartsAtUtc { get; set; }
    public Guid StartedByUserId { get; set; }
}
