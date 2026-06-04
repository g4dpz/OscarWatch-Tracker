using System.Net;
using System.Text;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class GitHubReleaseServiceTests
{
    private const string SampleJson = """
        {
          "tag_name": "v0.8.4",
          "html_url": "https://github.com/magicbug/OscarWatch-Tracker/releases/tag/v0.8.4",
          "name": "OscarWatch Tracker — Version 0.8.4",
          "published_at": "2026-06-04T14:52:00Z",
          "body": "## New features\n* Example"
        }
        """;

    [Theory]
    [InlineData("v0.8.4", 0, 8, 4)]
    [InlineData("0.8.4", 0, 8, 4)]
    [InlineData("v1.0.0+build", 1, 0, 0)]
    public void TryParseTag_parses_release_tags(string tag, int major, int minor, int build)
    {
        var version = ReleaseVersion.TryParseTag(tag);
        Assert.NotNull(version);
        Assert.Equal(major, version!.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(build, version.Build);
    }

    [Fact]
    public void IsNewer_compares_semver()
    {
        Assert.True(ReleaseVersion.IsNewer(new Version(0, 8, 4), new Version(0, 8, 3)));
        Assert.False(ReleaseVersion.IsNewer(new Version(0, 8, 3), new Version(0, 8, 3)));
    }

    [Fact]
    public async Task FetchLatestAsync_deserializes_github_response()
    {
        var handler = new StubHandler(SampleJson);
        var service = new GitHubReleaseService(new HttpClient(handler));

        var release = await service.FetchLatestAsync();

        Assert.Equal("v0.8.4", release.TagName);
        Assert.Contains("releases/tag/v0.8.4", release.HtmlUrl);
        Assert.Contains("0.8.4", release.Name);
        Assert.Contains("New features", release.Body);
    }

    [Fact]
    public async Task CheckForUpdateAsync_returns_available_when_behind()
    {
        var handler = new StubHandler(SampleJson);
        var service = new GitHubReleaseService(new HttpClient(handler));

        var result = await service.CheckForUpdateAsync(new Version(0, 8, 3));

        Assert.Equal(AppUpdateCheckResultKind.UpdateAvailable, result.Kind);
        Assert.Equal("v0.8.4", result.Release?.TagName);
    }

    [Fact]
    public async Task CheckForUpdateAsync_returns_up_to_date_when_current()
    {
        var handler = new StubHandler(SampleJson);
        var service = new GitHubReleaseService(new HttpClient(handler));

        var result = await service.CheckForUpdateAsync(new Version(0, 8, 4));

        Assert.Equal(AppUpdateCheckResultKind.UpToDate, result.Kind);
    }

    [Fact]
    public async Task FetchLatestAsync_uses_cache_within_ttl()
    {
        var handler = new StubHandler(SampleJson);
        var service = new GitHubReleaseService(new HttpClient(handler));

        await service.FetchLatestAsync();
        await service.FetchLatestAsync();

        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
