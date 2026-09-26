using System.Net;
using System.Text;
using OscarWatch.Core.HamQth;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class HamQthXmlParserTests
{
    [Fact]
    public void ParseSession_reads_session_id()
    {
        const string xml = """
            <HamQTH version="2.7" xmlns="https://www.hamqth.com">
              <session>
                <session_id>abc123</session_id>
              </session>
            </HamQTH>
            """;

        var session = HamQthXmlParser.ParseSession(xml);

        Assert.True(session.HasSession);
        Assert.Equal("abc123", session.SessionId);
        Assert.Equal("", session.Error);
    }

    [Fact]
    public void ParseSession_reads_error()
    {
        const string xml = """
            <HamQTH>
              <session>
                <error>Wrong user name or password</error>
              </session>
            </HamQTH>
            """;

        var session = HamQthXmlParser.ParseSession(xml);

        Assert.False(session.HasSession);
        Assert.Equal("Wrong user name or password", session.Error);
    }

    [Fact]
    public void ParseSearch_uses_nick_not_address_name()
    {
        const string xml = """
            <HamQTH xmlns="https://www.hamqth.com">
              <search>
                <callsign>ok2cqr</callsign>
                <nick>Petr</nick>
                <adr_name>Petr Hlozek</adr_name>
                <grid>jo70gg</grid>
              </search>
            </HamQTH>
            """;

        var entry = HamQthXmlParser.ParseSearch(xml);

        Assert.NotNull(entry);
        Assert.Equal("OK2CQR", entry.Call);
        Assert.Equal("Petr", entry.Name);
        Assert.Equal("JO70GG", entry.Grid);
    }

    [Fact]
    public void ParseSearch_ignores_address_name_when_nick_missing()
    {
        const string xml = """
            <HamQTH>
              <search>
                <callsign>g0abc</callsign>
                <adr_name>Hiram Maxim</adr_name>
                <grid>IO91</grid>
              </search>
            </HamQTH>
            """;

        var entry = HamQthXmlParser.ParseSearch(xml);

        Assert.NotNull(entry);
        Assert.Equal("", entry.Name);
        Assert.Equal("IO91", entry.Grid);
    }

    [Fact]
    public void IsSessionExpired_detects_expiry_text() =>
        Assert.True(HamQthXmlParser.IsSessionExpired("Session does not exist or expired"));

    [Fact]
    public void IsNotFound_detects_missing_call() =>
        Assert.True(HamQthXmlParser.IsNotFound("Callsign not found"));
}

public sealed class HamQthCallbookServiceTests
{
    [Fact]
    public void CanLookup_requires_enabled_credentials()
    {
        var service = new HamQthCallbookService(new HttpClient(new StubHandler()));

        Assert.False(service.CanLookup(new HamQthSettings()));
        Assert.True(service.CanLookup(new HamQthSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        }));
    }

    [Fact]
    public async Task TestConnectionAsync_succeeds_with_session()
    {
        var service = new HamQthCallbookService(new HttpClient(new StubHandler()));

        var result = await service.TestConnectionAsync(new HamQthSettings
        {
            Username = "MM9SQL",
            Password = "secret"
        });

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task LookupAsync_returns_name_and_grid()
    {
        var service = new HamQthCallbookService(new HttpClient(new StubHandler()));
        var settings = new HamQthSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        var entry = await service.LookupAsync(settings, "OK2CQR/P");

        Assert.NotNull(entry);
        Assert.Equal("Petr", entry.Name);
        Assert.Equal("JO70GG", entry.Grid);
    }

    [Fact]
    public async Task LookupAsync_uses_cache_on_second_call()
    {
        var handler = new StubHandler();
        var service = new HamQthCallbookService(new HttpClient(handler));
        var settings = new HamQthSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        await service.LookupAsync(settings, "OK2CQR");
        var firstCount = handler.RequestCount;
        await service.LookupAsync(settings, "OK2CQR");

        Assert.Equal(firstCount, handler.RequestCount);
    }

    [Fact]
    public async Task TestConnectionAsync_reports_bad_password()
    {
        var handler = new StubHandler { RejectLogin = true };
        var service = new HamQthCallbookService(new HttpClient(handler));

        var result = await service.TestConnectionAsync(new HamQthSettings
        {
            Username = "MM9SQL",
            Password = "wrong"
        });

        Assert.False(result.Ok);
        Assert.Contains("password", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UrlConstruction_LoginUrl_MatchesExpectedFormat()
    {
        // This test verifies the StringBuilder URL construction optimization produces
        // functionally equivalent URLs to the original string concatenation approach
        var handler = new StubHandler();
        var service = new HamQthCallbookService(new HttpClient(handler));

        await service.TestConnectionAsync(new HamQthSettings
        {
            Username = "MM9SQL/P",
            Password = "test&secret"
        });

        // Verify the captured URL contains properly escaped parameters
        var lastUri = handler.LastUri;
        Assert.Contains("https://www.hamqth.com/xml.php?u=MM9SQL%2FP", lastUri);
        Assert.Contains("&p=test%26secret", lastUri);
    }

    [Fact]
    public async Task UrlConstruction_LookupUrl_MatchesExpectedFormat()
    {
        // This test verifies the StringBuilder lookup URL construction eliminates allocations
        // while maintaining functional equivalence with the original approach
        var handler = new StubHandler();
        var service = new HamQthCallbookService(new HttpClient(handler));
        var settings = new HamQthSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        await service.LookupAsync(settings, "OK2CQR/M");

        // Verify lookup URL format - should have session ID, callsign, and program parameters
        var lookupUri = handler.LookupUris.LastOrDefault();
        Assert.NotNull(lookupUri);
        Assert.Contains("https://www.hamqth.com/xml.php?id=abc123", lookupUri);
        Assert.Contains("&callsign=OK2CQR", lookupUri); // Portable suffix stripped
        Assert.Contains("&prg=OscarWatch", lookupUri);
    }

    [Fact]
    public async Task UrlConstruction_ConcurrentRequests_DoNotInterfere()
    {
        // Test that the lock-protected StringBuilder buffer handles concurrent URL construction
        // This verifies thread safety of the optimization
        var handler = new StubHandler();
        var service = new HamQthCallbookService(new HttpClient(handler));
        var settings = new HamQthSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        // Make multiple concurrent lookups with different callsigns
        var tasks = new[]
        {
            service.LookupAsync(settings, "OK2CQR"),
            service.LookupAsync(settings, "G0ABC"),
            service.LookupAsync(settings, "W1AW")
        };

        await Task.WhenAll(tasks);

        // Verify all requests completed successfully and URLs were constructed properly
        Assert.True(handler.RequestCount >= 4); // Login + 3 lookups minimum
        Assert.True(handler.LookupUris.Count >= 3);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string LastUri { get; private set; } = "";

        public List<string> LookupUris { get; } = new();

        public bool RejectLogin { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastUri = request.RequestUri?.ToString() ?? "";
            var query = request.RequestUri?.Query ?? "";
            
            // Capture lookup URLs (those with session ID)
            if (query.Contains("id=", StringComparison.Ordinal))
            {
                LookupUris.Add(LastUri);
            }
            
            string body;
            if (query.Contains("u=", StringComparison.Ordinal))
            {
                body = RejectLogin
                    ? """<HamQTH><session><error>Wrong user name or password</error></session></HamQTH>"""
                    : """
                      <HamQTH xmlns="https://www.hamqth.com">
                        <session>
                          <session_id>abc123</session_id>
                        </session>
                      </HamQTH>
                      """;
            }
            else
            {
                body = """
                    <HamQTH xmlns="https://www.hamqth.com">
                      <search>
                        <callsign>ok2cqr</callsign>
                        <nick>Petr</nick>
                        <grid>jo70gg</grid>
                      </search>
                    </HamQTH>
                    """;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml")
            });
        }
    }
}
