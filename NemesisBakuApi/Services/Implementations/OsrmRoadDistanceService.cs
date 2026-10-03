using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public sealed class OsrmRoadDistanceService(HttpClient http, IOptions<RoadRoutingSettings> options) : IRoadDistanceService
{
    public async Task<IReadOnlyList<decimal?>> GetDistancesKmAsync(IReadOnlyList<RoadPoint> origins, RoadPoint destination, CancellationToken ct)
    {
        if (origins.Count == 0) return [];
        if (!Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var server) ||
            (server.Scheme != "http" && server.Scheme != "https") || server.UserInfo.Length != 0 ||
            server.Query.Length != 0 || server.Fragment.Length != 0)
            throw new RoadRoutingException("Yol məsafəsi xidməti hazır deyil. Mağaza ilə əlaqə saxlayın.");
        foreach (var p in origins.Append(destination))
            if (p.Latitude is < -90 or > 90 || p.Longitude is < -180 or > 180)
                throw new ArgumentException("Koordinatlar düzgün deyil.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.TimeoutSeconds, 1, 30)));
        try
        {
            var result = new List<decimal?>(origins.Count);
            // Keep each table below OSRM's default 100-coordinate limit.
            foreach (var batch in origins.Chunk(99))
            {
                var points = batch.Append(destination).ToArray();
                var coordinates = string.Join(";", points.Select(p =>
                    p.Longitude.ToString(CultureInfo.InvariantCulture) + "," + p.Latitude.ToString(CultureInfo.InvariantCulture)));
                var sources = string.Join(";", Enumerable.Range(0, batch.Length));
                var radiuses = string.Join(";", points.Select(_ => Math.Clamp(options.Value.MaxSnapDistanceMeters, 1, 500).ToString(CultureInfo.InvariantCulture)));
                var url = server.AbsoluteUri.TrimEnd('/') + "/table/v1/driving/" + coordinates +
                    $"?sources={sources}&destinations={batch.Length}&annotations=distance&radiuses={radiuses}&generate_hints=false";
                // Never request fallback_speed: OSRM would estimate unreachable routes by straight line.
                using var response = await http.GetAsync(url, timeout.Token);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                var root = json.RootElement;
                if (root.GetProperty("code").GetString() != "Ok") throw new JsonException();
                if (root.TryGetProperty("fallback_speed_cells", out var fallback) &&
                    (fallback.ValueKind != JsonValueKind.Array || fallback.GetArrayLength() != 0)) throw new JsonException();
                var rows = root.GetProperty("distances");
                if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != batch.Length) throw new JsonException();
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 1) throw new JsonException();
                    var value = row[0];
                    if (value.ValueKind == JsonValueKind.Null) { result.Add(null); continue; }
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var meters) || meters < 0) throw new JsonException();
                    result.Add(meters / 1000m);
                }
            }
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new RoadRoutingException("Yol məsafəsi hesablanması gecikdi. Yenidən cəhd edin."); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { throw new RoadRoutingException("Yol məsafəsi alınmadı. Yenidən cəhd edin.", ex); }
    }
}
