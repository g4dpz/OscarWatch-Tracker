using System.Net;
using System.Text;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class HamsAtRovesServiceTests
{
    private const string SampleJson = """
        {
          "data": [
            {
              "id": "9c465415-5b3d-4951-a5b7-93bb9346abef",
              "mode": "CW",
              "comment": "Will be on SSB if CWs are all solved",
              "url": "https://hams.at/alerts/9c465415-5b3d-4951-a5b7-93bb9346abef",
              "callsign": "BA8AFK/0",
              "aos_at": "2026-06-07T02:38:50Z",
              "los_at": "2026-06-07T02:53:40Z",
              "grids": ["NM48", "NM49"],
              "mhz": 145.953,
              "is_workable": false,
              "satellite": { "name": "FO-29", "number": 24278 }
            }
          ]
        }
        """;

    [Fact]
    public async Task FetchUpcomingAsync_deserializes_alerts()
    {
        var handler = new StubHandler(SampleJson, HttpStatusCode.OK);
        var service = new HamsAtRovesService(new HttpClient(handler));
        var settings = new HamsAtSettings { ApiKey = "test-key" };

        var result = await service.FetchUpcomingAsync(settings, bypassCache: true);

        Assert.True(result.Ok);
        Assert.Single(result.Alerts);
        var alert = result.Alerts[0];
        Assert.Equal("BA8AFK/0", alert.Callsign);
        Assert.Equal("FO-29", alert.Satellite?.Name);
        Assert.Equal(2, alert.Grids.Count);
        Assert.Equal("Will be on SSB if CWs are all solved", alert.Comment);
        Assert.False(alert.IsWorkable);
    }

    [Fact]
    public async Task FetchUpcomingAsync_maps_is_workable_per_alert()
    {
        const string json = """
            {
              "data": [
                {
                  "id": "a",
                  "callsign": "W1AW",
                  "aos_at": "2026-06-07T02:38:50Z",
                  "los_at": "2026-06-07T02:53:40Z",
                  "grids": ["FN31"],
                  "is_workable": true
                },
                {
                  "id": "b",
                  "callsign": "W2ABC",
                  "aos_at": "2026-06-07T03:38:50Z",
                  "los_at": "2026-06-07T03:53:40Z",
                  "grids": ["EM12"],
                  "is_workable": false
                }
              ]
            }
            """;

        var service = new HamsAtRovesService(new HttpClient(new StubHandler(json)));
        var result = await service.FetchUpcomingAsync(
            new HamsAtSettings { ApiKey = "test-key" },
            bypassCache: true);

        Assert.True(result.Ok);
        Assert.Equal(2, result.Alerts.Count);
        Assert.Single(result.Alerts, a => a.IsWorkable);
        Assert.Equal("W1AW", result.Alerts.First(a => a.IsWorkable).Callsign);
    }

    [Fact]
    public async Task FetchUpcomingAsync_returns_error_for_unauthorized()
    {
        var handler = new StubHandler("{}", HttpStatusCode.Unauthorized);
        var service = new HamsAtRovesService(new HttpClient(handler));
        var settings = new HamsAtSettings { ApiKey = "bad-key" };

        var result = await service.FetchUpcomingAsync(settings, bypassCache: true);

        Assert.False(result.Ok);
        Assert.Contains("API key", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchUpcomingAsync_requires_api_key()
    {
        var service = new HamsAtRovesService(new HttpClient(new StubHandler(SampleJson)));
        var result = await service.FetchUpcomingAsync(new HamsAtSettings(), bypassCache: true);
        Assert.False(result.Ok);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HamsAtRovesService.UpcomingAlertsUrl, request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
