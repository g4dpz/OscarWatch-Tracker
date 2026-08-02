using System.Net;
using System.Text;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class SatelliteStatusReportServiceTests
{
    private static SatelliteStatusSettings ValidSettings => new()
    {
        Enabled = true,
        BaseUrl = "https://oscarwatch.org",
        ApiToken = "test-token"
    };

    [Fact]
    public async Task TestTokenAsync_succeeds_on_200()
    {
        var handler = new StubHandler("""{"callsign":"2M0SQL"}""", HttpStatusCode.OK);
        var service = new SatelliteStatusReportService(new HttpClient(handler));

        var result = await service.TestTokenAsync(ValidSettings);

        Assert.True(result.Ok);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("https://oscarwatch.org/api/v1/me", handler.LastUri?.ToString());
        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Equal("test-token", handler.LastAuthParameter);
    }

    [Fact]
    public async Task TestTokenAsync_fails_on_401()
    {
        var handler = new StubHandler("""{"message":"Unauthenticated."}""", HttpStatusCode.Unauthorized);
        var service = new SatelliteStatusReportService(new HttpClient(handler));

        var result = await service.TestTokenAsync(ValidSettings);

        Assert.False(result.Ok);
        Assert.Equal(401, result.HttpStatusCode);
        Assert.Contains("Unauthorised", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestTokenAsync_requires_token()
    {
        var service = new SatelliteStatusReportService(new HttpClient(new StubHandler("{}")));
        var result = await service.TestTokenAsync(new SatelliteStatusSettings { BaseUrl = "https://oscarwatch.org" });
        Assert.False(result.Ok);
        Assert.Equal(0, result.HttpStatusCode);
    }

    [Fact]
    public async Task SubmitReportAsync_returns_stored_on_201()
    {
        var handler = new StubHandler("{}", HttpStatusCode.Created);
        var service = new SatelliteStatusReportService(new HttpClient(handler));
        var request = new SatelliteStatusReportRequest(
            "AO-07",
            "Mode B",
            SatelliteStatusValue.On,
            new DateTime(2026, 7, 14, 20, 15, 0, DateTimeKind.Utc),
            "JO01",
            "OscarWatch-Tracker/1.0");

        var result = await service.SubmitReportAsync(ValidSettings, request);

        Assert.True(result.Ok);
        Assert.True(result.Stored);
        Assert.Equal(201, result.HttpStatusCode);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("https://oscarwatch.org/api/v1/satellite-status/reports", handler.LastUri?.ToString());
        Assert.Contains("\"satellite\":\"AO-07\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"Mode B\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"on\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"observed_at\":\"2026-07-14T20:15:00Z\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"gridsquare\":\"JO01\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"client\":\"OscarWatch-Tracker/1.0\"", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitReportAsync_returns_duplicate_on_200()
    {
        var handler = new StubHandler("{}", HttpStatusCode.OK);
        var service = new SatelliteStatusReportService(new HttpClient(handler));
        var request = new SatelliteStatusReportRequest(
            "AO-07",
            "Mode B",
            SatelliteStatusValue.Off,
            DateTime.UtcNow);

        var result = await service.SubmitReportAsync(ValidSettings, request);

        Assert.True(result.Ok);
        Assert.False(result.Stored);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Contains("Duplicate", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"status\":\"off\"", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitReportAsync_maps_telemetry_only()
    {
        var handler = new StubHandler("{}", HttpStatusCode.Created);
        var service = new SatelliteStatusReportService(new HttpClient(handler));
        var request = new SatelliteStatusReportRequest(
            "AO-07",
            "Mode B",
            SatelliteStatusValue.TelemetryOnly,
            DateTime.UtcNow);

        await service.SubmitReportAsync(ValidSettings, request);

        Assert.Contains("\"status\":\"telemetry_only\"", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitReportAsync_surfaces_403()
    {
        var handler = new StubHandler(
            """{"message":"Profile incomplete."}""",
            HttpStatusCode.Forbidden);
        var service = new SatelliteStatusReportService(new HttpClient(handler));
        var request = new SatelliteStatusReportRequest(
            "AO-07",
            "Mode B",
            SatelliteStatusValue.On,
            DateTime.UtcNow);

        var result = await service.SubmitReportAsync(ValidSettings, request);

        Assert.False(result.Ok);
        Assert.Equal(403, result.HttpStatusCode);
        Assert.Contains("Forbidden", result.Message, StringComparison.Ordinal);
        Assert.Contains("Profile incomplete", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitReportAsync_surfaces_422()
    {
        var handler = new StubHandler(
            """{"message":"The mode field is invalid.","errors":{"mode":["Unknown mode."]}}""",
            HttpStatusCode.UnprocessableEntity);
        var service = new SatelliteStatusReportService(new HttpClient(handler));
        var request = new SatelliteStatusReportRequest(
            "AO-07",
            "Bad",
            SatelliteStatusValue.On,
            DateTime.UtcNow);

        var result = await service.SubmitReportAsync(ValidSettings, request);

        Assert.False(result.Ok);
        Assert.Equal(422, result.HttpStatusCode);
        Assert.Contains("Validation failed", result.Message, StringComparison.Ordinal);
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

        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastUri { get; private set; }
        public string? LastAuthScheme { get; private set; }
        public string? LastAuthParameter { get; private set; }
        public string LastBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastUri = request.RequestUri;
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            LastAuthParameter = request.Headers.Authorization?.Parameter;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
