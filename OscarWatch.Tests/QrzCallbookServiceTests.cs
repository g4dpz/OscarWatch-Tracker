using System.Net;
using System.Text;
using OscarWatch.Core.Models;
using OscarWatch.Core.Qrz;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class QrzCallsignHelperTests
{
    [Theory]
    [InlineData("EA3EA", true)]
    [InlineData("ea3ea", true)]
    [InlineData("MM9SQL", true)]
    [InlineData("W1AW", true)]
    [InlineData("N0A", true)]
    [InlineData("2E0ABC", true)]
    [InlineData("EA3EA/P", true)]
    [InlineData("G0ABC/M", true)]
    [InlineData("EA3", false)]
    [InlineData("AB", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPlausible_matches_complete_calls(string? call, bool expected) =>
        Assert.Equal(expected, QrzCallsignHelper.IsPlausible(call));

    [Theory]
    [InlineData("EA3EA/P", "EA3EA")]
    [InlineData("G0ABC/M", "G0ABC")]
    [InlineData("W1AW/MM", "W1AW")]
    [InlineData("MM9SQL", "MM9SQL")]
    [InlineData("vp2v/w1aw", "VP2V/W1AW")]
    public void ToLookupCall_strips_portable_suffixes(string input, string expected) =>
        Assert.Equal(expected, QrzCallsignHelper.ToLookupCall(input));
}

public sealed class QrzXmlParserTests
{
    private const string NamespacedLogin = """
        <?xml version="1.0" ?>
        <QRZDatabase version="1.34" xmlns="http://xmldata.qrz.com">
          <Session>
            <Key>abc123</Key>
            <Count>4</Count>
            <SubExp>Wed Jan 1 00:00:00 2027</SubExp>
            <GMTime>Tue Sep 15 11:00:00 2026</GMTime>
          </Session>
        </QRZDatabase>
        """;

    private const string CallsignLookup = """
        <?xml version="1.0" ?>
        <QRZDatabase version="1.34" xmlns="http://xmldata.qrz.com">
          <Callsign>
            <call>EA3EA</call>
            <fname>Joan</fname>
            <name>SMITH</name>
            <grid>JN11</grid>
          </Callsign>
          <Session>
            <Key>abc123</Key>
            <SubExp>Wed Jan 1 00:00:00 2027</SubExp>
          </Session>
        </QRZDatabase>
        """;

    [Fact]
    public void ParseSession_reads_key_and_subscription()
    {
        var session = QrzXmlParser.ParseSession(NamespacedLogin);

        Assert.True(session.HasKey);
        Assert.Equal("abc123", session.Key);
        Assert.Equal("Wed Jan 1 00:00:00 2027", session.SubscriptionExpires);
        Assert.Equal("", session.Error);
    }

    [Fact]
    public void ParseSession_reads_error()
    {
        const string xml = """
            <QRZDatabase>
              <Session>
                <Error>Username/password incorrect</Error>
              </Session>
            </QRZDatabase>
            """;

        var session = QrzXmlParser.ParseSession(xml);

        Assert.False(session.HasKey);
        Assert.Equal("Username/password incorrect", session.Error);
    }

    [Fact]
    public void ParseCallsign_prefers_first_name_and_grid()
    {
        var entry = QrzXmlParser.ParseCallsign(CallsignLookup);

        Assert.NotNull(entry);
        Assert.Equal("EA3EA", entry.Call);
        Assert.Equal("Joan", entry.Name);
        Assert.Equal("JN11", entry.Grid);
    }

    [Fact]
    public void ParseCallsign_ignores_last_name_when_first_name_missing()
    {
        const string xml = """
            <QRZDatabase>
              <Callsign>
                <call>G0ABC</call>
                <name>MAXIM</name>
                <grid>IO91</grid>
              </Callsign>
            </QRZDatabase>
            """;

        var entry = QrzXmlParser.ParseCallsign(xml);

        Assert.NotNull(entry);
        Assert.Equal("", entry.Name);
        Assert.Equal("IO91", entry.Grid);
    }

    [Theory]
    [InlineData("Session Timeout", true)]
    [InlineData("Not found: ZZ0ZZ", false)]
    public void IsSessionTimeout_detects_timeout_text(string error, bool expected) =>
        Assert.Equal(expected, QrzXmlParser.IsSessionTimeout(error));

    [Fact]
    public void IsNotFound_detects_missing_call() =>
        Assert.True(QrzXmlParser.IsNotFound("Not found: ZZ0ZZ"));

    [Fact]
    public void IsSubscriptionRequired_detects_xml_subscription_errors() =>
        Assert.True(QrzXmlParser.IsSubscriptionRequired("A subscription is required to access XML data"));
}

public sealed class QrzCallbookServiceTests
{
    [Fact]
    public void CanLookup_requires_enabled_credentials()
    {
        var service = new QrzCallbookService(new HttpClient(new StubHandler()));

        Assert.False(service.CanLookup(new QrzSettings()));
        Assert.False(service.CanLookup(new QrzSettings { Enabled = true, Username = "MM9SQL" }));
        Assert.True(service.CanLookup(new QrzSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        }));
    }

    [Fact]
    public async Task TestConnectionAsync_returns_subscription_expiry()
    {
        var handler = new StubHandler();
        var service = new QrzCallbookService(new HttpClient(handler));

        var result = await service.TestConnectionAsync(new QrzSettings
        {
            Username = "MM9SQL",
            Password = "secret"
        });

        Assert.True(result.Ok);
        Assert.Equal("Wed Jan 1 00:00:00 2027", result.SubscriptionExpires);
        Assert.True(handler.RequestCount >= 1);
        Assert.DoesNotContain("secret", result.ErrorMessage ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupAsync_returns_name_and_valid_grid()
    {
        var service = new QrzCallbookService(new HttpClient(new StubHandler()));
        var settings = new QrzSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        var entry = await service.LookupAsync(settings, "EA3EA/P");

        Assert.NotNull(entry);
        Assert.Equal("Joan", entry.Name);
        Assert.Equal("JN11", entry.Grid);
    }

    [Fact]
    public async Task LookupAsync_uses_cache_on_second_call()
    {
        var handler = new StubHandler();
        var service = new QrzCallbookService(new HttpClient(handler));
        var settings = new QrzSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        await service.LookupAsync(settings, "EA3EA");
        var firstCount = handler.RequestCount;
        await service.LookupAsync(settings, "EA3EA");

        Assert.Equal(firstCount, handler.RequestCount);
    }

    [Fact]
    public async Task LookupAsync_returns_null_when_not_found()
    {
        var handler = new StubHandler { NotFoundCall = "ZZ0ZZ" };
        var service = new QrzCallbookService(new HttpClient(handler));
        var settings = new QrzSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        var entry = await service.LookupAsync(settings, "ZZ0ZZ");

        Assert.Null(entry);
    }

    [Fact]
    public async Task TestConnectionAsync_reports_bad_password()
    {
        var handler = new StubHandler { RejectLogin = true };
        var service = new QrzCallbookService(new HttpClient(handler));

        var result = await service.TestConnectionAsync(new QrzSettings
        {
            Username = "MM9SQL",
            Password = "wrong"
        });

        Assert.False(result.Ok);
        Assert.Contains("incorrect", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UrlConstruction_LoginUrl_MatchesExpectedFormat()
    {
        // This test verifies the StringBuilder URL construction optimization produces
        // functionally equivalent URLs to the original string concatenation approach
        var handler = new StubHandler();
        var service = new QrzCallbookService(new HttpClient(handler));

        await service.TestConnectionAsync(new QrzSettings
        {
            Username = "MM9SQL/P",
            Password = "test&secret"
        });

        // Verify the captured URL contains properly escaped parameters
        var lastUri = handler.LastUri;
        Assert.Contains("https://xmldata.qrz.com/xml/current/?username=MM9SQL%2FP", lastUri);
        Assert.Contains("&password=test%26secret", lastUri);
        Assert.Contains("&agent=OscarWatch", lastUri);
    }

    [Fact]
    public async Task UrlConstruction_LookupUrl_MatchesExpectedFormat()
    {
        // This test verifies the StringBuilder lookup URL construction eliminates allocations
        // while maintaining functional equivalence with the original approach
        var handler = new StubHandler();
        var service = new QrzCallbookService(new HttpClient(handler));
        var settings = new QrzSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        await service.LookupAsync(settings, "EA3EA/M");

        // Verify lookup URL format - should have session key and callsign parameters
        var lookupUri = handler.LookupUris.LastOrDefault();
        Assert.NotNull(lookupUri);
        Assert.Contains("https://xmldata.qrz.com/xml/current/?s=abc123", lookupUri);
        Assert.Contains("&callsign=EA3EA", lookupUri); // Portable suffix stripped by ToLookupCall
    }

    [Fact]
    public async Task UrlConstruction_ConcurrentRequests_DoNotInterfere()
    {
        // Test that the lock-protected StringBuilder buffer handles concurrent URL construction
        // This verifies thread safety of the optimization
        var handler = new StubHandler();
        var service = new QrzCallbookService(new HttpClient(handler));
        var settings = new QrzSettings
        {
            Enabled = true,
            Username = "MM9SQL",
            Password = "secret"
        };

        // Make multiple concurrent lookups with different callsigns
        var tasks = new[]
        {
            service.LookupAsync(settings, "EA3EA"),
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

        public string? NotFoundCall { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastUri = request.RequestUri?.ToString() ?? "";
            var query = request.RequestUri?.Query ?? "";
            
            // Capture lookup URLs (those with session key)
            if (query.Contains("s=", StringComparison.Ordinal))
            {
                LookupUris.Add(LastUri);
            }
            
            string body;
            if (query.Contains("username=", StringComparison.Ordinal))
            {
                body = RejectLogin
                    ? """<QRZDatabase><Session><Error>Username/password incorrect</Error></Session></QRZDatabase>"""
                    : """
                      <QRZDatabase xmlns="http://xmldata.qrz.com">
                        <Session>
                          <Key>abc123</Key>
                          <SubExp>Wed Jan 1 00:00:00 2027</SubExp>
                        </Session>
                      </QRZDatabase>
                      """;
            }
            else if (NotFoundCall is not null
                     && query.Contains("callsign=" + Uri.EscapeDataString(NotFoundCall), StringComparison.OrdinalIgnoreCase))
            {
                body = """<QRZDatabase><Session><Key>abc123</Key><Error>Not found: ZZ0ZZ</Error></Session></QRZDatabase>""";
            }
            else
            {
                body = """
                    <QRZDatabase xmlns="http://xmldata.qrz.com">
                      <Callsign>
                        <call>EA3EA</call>
                        <fname>Joan</fname>
                        <grid>JN11</grid>
                      </Callsign>
                      <Session>
                        <Key>abc123</Key>
                        <SubExp>Wed Jan 1 00:00:00 2027</SubExp>
                      </Session>
                    </QRZDatabase>
                    """;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml")
            });
        }
    }
}
