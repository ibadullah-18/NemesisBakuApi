using System.Net;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Implementations;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;
using Xunit;

namespace NemesisBakuApi.Tests;

public class OsrmRoadDistanceTests
{
    private sealed class Handler(string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(response) });
        }
    }
    private static OsrmRoadDistanceService Service(Handler handler, string url = "http://osrm:5000") =>
        new(new HttpClient(handler), Options.Create(new RoadRoutingSettings { BaseUrl = url }));

    [Fact]
    public async Task Matrix_uses_origin_to_customer_road_meters_and_longitude_first()
    {
        using var handler = new Handler("""{"code":"Ok","distances":[[900],[1400]]}""");
        var distances = await Service(handler).GetDistancesKmAsync([new(40.4m, 49.8m), new(40.3m, 49.9m)], new(40.5m, 49.7m), default);
        Assert.Equal(new decimal?[] { .9m, 1.4m }, distances);
        var url = Uri.UnescapeDataString(Assert.Single(handler.Requests).AbsoluteUri);
        Assert.Contains("/table/v1/driving/49.8,40.4;49.9,40.3;49.7,40.5?", url);
        Assert.Contains("sources=0;1&destinations=2&annotations=distance", url);
        Assert.Contains("radiuses=100;100;100", url);
        Assert.DoesNotContain("fallback_speed", url);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"code\":\"Ok\",\"distances\":[]}")]
    [InlineData("{\"code\":\"Ok\",\"distances\":[[-1]]}")]
    [InlineData("{\"code\":\"Ok\",\"distances\":[[900,1000]]}")]
    [InlineData("{\"code\":\"Ok\",\"distances\":[[\"900\"]]}")]
    [InlineData("{\"code\":\"NoRoute\"}")]
    [InlineData("{\"code\":\"Ok\",\"distances\":[[900]],\"fallback_speed_cells\":[[0,0]]}")]
    [InlineData("not-json")]
    public async Task Invalid_or_partial_responses_never_return_estimated_distance(string response)
    {
        using var handler = new Handler(response);
        await Assert.ThrowsAsync<RoadRoutingException>(() => Service(handler).GetDistancesKmAsync([new(40, 49)], new(41, 50), default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://osrm")]
    [InlineData("http://osrm?key=test")]
    public async Task Missing_or_invalid_server_blocks_without_network(string url)
    {
        using var handler = new Handler("{}");
        await Assert.ThrowsAsync<RoadRoutingException>(() => Service(handler, url).GetDistancesKmAsync([new(40, 49)], new(41, 50), default));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task No_road_is_null_and_zero_is_valid()
    {
        using var handler = new Handler("""{"code":"Ok","distances":[[null],[0]]}""");
        Assert.Equal(new decimal?[] { null, 0 }, await Service(handler).GetDistancesKmAsync([new(40, 49), new(41, 50)], new(41, 50), default));
    }

    [Fact]
    public async Task Server_failure_blocks_pricing()
    {
        using var handler = new Handler("{}", HttpStatusCode.ServiceUnavailable);
        await Assert.ThrowsAsync<RoadRoutingException>(() => Service(handler).GetDistancesKmAsync([new(40, 49)], new(41, 50), default));
    }
}
