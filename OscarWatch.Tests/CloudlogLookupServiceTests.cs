using System.Net;
using System.Text;
using OscarWatch.Cloudlog;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class CloudlogLookupServiceTests
{
    private const string LogbooksJson = """
        {
          "status": "success",
          "count": 2,
          "logbooks": [
            {
              "logbook_id": 1,
              "logbook_name": "Main Log",
              "public_slug": "m0abc",
              "access_level": "owner"
            },
            {
              "logbook_id": 2,
              "logbook_name": "Portable",
              "public_slug": "m0abc-p",
              "access_level": "shared"
            }
          ]
        }
        """;

    [Fact]
    public async Task FetchLogbooksAsync_deserializes_logbooks()
    {
        var handler = new StubHandler(LogbooksJson);
        var service = new CloudlogLookupService(new HttpClient(handler));
        var settings = new CloudlogSettings
        {
            BaseUrl = "https://cloudlog.example",
            ApiKey = "test-key"
        };

        var result = await service.FetchLogbooksAsync(settings);

        Assert.True(result.Ok);
        Assert.Equal(2, result.Logbooks.Count);
        Assert.Equal("m0abc", result.Logbooks[0].PublicSlug);
        Assert.Equal("Main Log", result.Logbooks[0].LogbookName);
    }

    [Fact]
    public async Task CheckGridWorkedAsync_maps_found_and_not_found()
    {
        var handler = new GridCheckStubHandler(
            """{"gridsquare":"FN20","result":"Not Found"}""",
            HttpStatusCode.Created);
        var service = new CloudlogLookupService(new HttpClient(handler));
        var settings = new CloudlogSettings
        {
            Enabled = true,
            CheckRoveGrids = true,
            BaseUrl = "https://cloudlog.example",
            ApiKey = "test-key",
            LogbookPublicSlug = "m0abc"
        };

        var result = await service.CheckGridWorkedAsync(settings, "fn20");

        Assert.NotNull(result);
        Assert.Equal("FN20", result!.Grid);
        Assert.False(result.IsWorked);
        Assert.Contains("\"band\":\"SAT\"", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanCheckGrids_requires_logbook_slug()
    {
        var service = new CloudlogLookupService(new HttpClient(new StubHandler("{}")));
        Assert.False(service.CanCheckGrids(new CloudlogSettings
        {
            Enabled = true,
            CheckRoveGrids = true,
            BaseUrl = "https://cloudlog.example",
            ApiKey = "key"
        }));
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
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class GridCheckStubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public string? LastRequestBody { get; private set; }

        public GridCheckStubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
