using System.Net;
using System.Text;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class SatelliteStatusCommunityFetchTests
{
    private const string SampleJson = """
        {
          "satellites": [
            {
              "name": "AO-07",
              "modes": [
                {
                  "type": "Mode B",
                  "status": "on",
                  "status_label": "On",
                  "report_count": 3,
                  "recent_reports": [
                    {
                      "callsign": "2M0SQL",
                      "gridsquare": "IO91UK",
                      "status": "on",
                      "observed_at": "2026-07-14T20:15:00+00:00"
                    },
                    {
                      "callsign": "W1AW",
                      "gridsquare": "FN31",
                      "status": "off",
                      "observed_at": "2026-07-14T18:00:00+00:00"
                    }
                  ]
                },
                {
                  "type": "Mode A",
                  "status": null,
                  "status_label": null,
                  "report_count": 0,
                  "recent_reports": []
                }
              ]
            }
          ],
          "window_hours": 24,
          "server_time_utc": "2026-07-14T21:00:00+00:00"
        }
        """;

    [Fact]
    public void ParseCommunityCatalog_maps_modes_and_newest_report()
    {
        var catalog = SatelliteStatusReportService.ParseCommunityCatalog(
            SampleJson,
            new DateTime(2026, 7, 14, 21, 0, 0, DateTimeKind.Utc));

        Assert.NotNull(catalog);
        Assert.Equal(24, catalog.WindowHours);
        var modeB = catalog.TryGetMode("AO-07", "Mode B");
        Assert.NotNull(modeB);
        Assert.Equal(SatelliteCommunityStatusKind.On, modeB.Kind);
        Assert.Equal(3, modeB.ReportCount);
        Assert.Equal(new DateTime(2026, 7, 14, 20, 15, 0, DateTimeKind.Utc), modeB.NewestReportUtc);

        var modeA = catalog.TryGetMode("ao-07", "mode a");
        Assert.NotNull(modeA);
        Assert.Equal(SatelliteCommunityStatusKind.Unknown, modeA.Kind);
    }

    [Fact]
    public async Task FetchCommunityAsync_returns_catalog_on_200()
    {
        var handler = new StubHandler(SampleJson, HttpStatusCode.OK);
        var service = new SatelliteStatusReportService(new HttpClient(handler));
        var result = await service.FetchCommunityAsync(new SatelliteStatusSettings
        {
            BaseUrl = "https://oscarwatch.org"
        });

        Assert.True(result.Ok);
        Assert.False(result.FeatureUnavailable);
        Assert.NotNull(result.Catalog);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("https://oscarwatch.org/api/v1/satellite-status", handler.LastUri?.ToString());
        Assert.Null(handler.LastAuthScheme);
    }

    [Fact]
    public async Task FetchCommunityAsync_marks_feature_unavailable_on_404()
    {
        var handler = new StubHandler("{}", HttpStatusCode.NotFound);
        var service = new SatelliteStatusReportService(new HttpClient(handler));
        var result = await service.FetchCommunityAsync(new SatelliteStatusSettings
        {
            BaseUrl = "https://oscarwatch.org"
        });

        Assert.False(result.Ok);
        Assert.True(result.FeatureUnavailable);
    }

    [Fact]
    public async Task FetchCommunityAsync_soft_fails_on_network_error()
    {
        var service = new SatelliteStatusReportService(new HttpClient(new ThrowingHandler()));
        var result = await service.FetchCommunityAsync(new SatelliteStatusSettings
        {
            BaseUrl = "https://oscarwatch.org"
        });

        Assert.False(result.Ok);
        Assert.False(result.FeatureUnavailable);
        Assert.Null(result.Catalog);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(-4.0, true)]
    [InlineData(-2.9, false)]
    [InlineData(0.0, false)]
    public void IsStale_uses_three_hour_threshold(double? hoursAgo, bool expectedStale)
    {
        var now = new DateTime(2026, 7, 14, 21, 0, 0, DateTimeKind.Utc);
        DateTime? newest = hoursAgo is null ? null : now.AddHours(hoursAgo.Value);
        Assert.Equal(expectedStale, SatelliteStatusCommunityPresentation.IsStale(newest, now));
    }

    [Fact]
    public void RefreshInterval_is_shorter_than_cache_ttl()
    {
        Assert.True(
            SatelliteStatusCommunityPresentation.RefreshInterval < SatelliteStatusCommunityPresentation.CacheTtl);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(10.1, false)]
    public void IsCacheFresh_uses_ten_minute_ttl(double minutesAgo, bool expectedFresh)
    {
        var now = new DateTime(2026, 7, 14, 21, 0, 0, DateTimeKind.Utc);
        var fetchedAt = now.AddMinutes(-minutesAgo);
        Assert.Equal(expectedFresh, SatelliteStatusCommunityPresentation.IsCacheFresh(fetchedAt, now));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(4.9, false)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    public void IsRefreshDue_uses_five_minute_interval(double minutesAgo, bool expectedDue)
    {
        var now = new DateTime(2026, 7, 14, 21, 0, 0, DateTimeKind.Utc);
        var fetchedAt = now.AddMinutes(-minutesAgo);
        Assert.Equal(expectedDue, SatelliteStatusCommunityPresentation.IsRefreshDue(fetchedAt, now));
    }

    [Fact]
    public void ResolvePassRowModeType_prefers_frequency_selection()
    {
        var database = new StubDatabase(
        [
            new SatelliteRadioEntry
            {
                Name = "AO-07",
                Modes =
                [
                    new SatelliteTransponderMode { Type = "Mode B", UplinkKHz = 432, DownlinkKHz = 145 },
                    new SatelliteTransponderMode { Type = "Mode A", UplinkKHz = 145, DownlinkKHz = 29 }
                ]
            }
        ]);

        var selections = new Dictionary<string, SatelliteFrequencySelection>(StringComparer.OrdinalIgnoreCase)
        {
            ["AO-07"] = new SatelliteFrequencySelection { ModeType = "Mode A" }
        };

        var resolved = SatelliteStatusCommunityPresentation.ResolvePassRowModeType(
            "AO-07",
            "7530",
            selections,
            database);

        Assert.Equal("Mode A", resolved);
    }

    [Fact]
    public void ResolvePassRowModeType_defaults_to_first_non_beacon()
    {
        var database = new StubDatabase(
        [
            new SatelliteRadioEntry
            {
                Name = "AO-07",
                Modes =
                [
                    new SatelliteTransponderMode { Type = "CW", UplinkKHz = 0, DownlinkKHz = 145 },
                    new SatelliteTransponderMode { Type = "Mode B", UplinkKHz = 432, DownlinkKHz = 145 }
                ]
            }
        ]);

        var resolved = SatelliteStatusCommunityPresentation.ResolvePassRowModeType(
            "AO-07",
            null,
            new Dictionary<string, SatelliteFrequencySelection>(StringComparer.OrdinalIgnoreCase),
            database);

        Assert.Equal("Mode B", resolved);
    }

    [Fact]
    public void FormatAge_formats_minutes_and_hours()
    {
        var now = new DateTime(2026, 7, 14, 21, 0, 0, DateTimeKind.Utc);
        Assert.Equal("12m ago", SatelliteStatusCommunityPresentation.FormatAge(now.AddMinutes(-12), now));
        Assert.Equal("6h ago", SatelliteStatusCommunityPresentation.FormatAge(now.AddHours(-6), now));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public StubHandler(string body, HttpStatusCode status)
        {
            _body = body;
            _status = status;
        }

        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastUri { get; private set; }
        public string? LastAuthScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastUri = request.RequestUri;
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    private sealed class StubDatabase(IReadOnlyList<SatelliteRadioEntry> entries) : ISatelliteDatabaseService
    {
        public IReadOnlyList<SatelliteRadioEntry> Entries => entries;
        public string ActiveDatabasePath => "";
        public bool IsUsingUserDatabase => false;
        public void Reload() { }

        public SatelliteRadioEntry? TryGetEntry(string satelliteName, string? noradId = null) =>
            entries.FirstOrDefault(e =>
                string.Equals(e.Name, satelliteName, StringComparison.OrdinalIgnoreCase));
    }
}
