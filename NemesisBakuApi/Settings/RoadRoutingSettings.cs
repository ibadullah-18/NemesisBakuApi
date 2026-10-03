namespace NemesisBakuApi.Settings;

public sealed class RoadRoutingSettings
{
    // Private, self-hosted OSRM endpoint. No public demo server fallback.
    public string BaseUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxSnapDistanceMeters { get; set; } = 100;
}
