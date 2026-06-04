namespace OscarWatch.Core.Models;

public sealed class GitHubLatestRelease
{
    public required string TagName { get; init; }
    public required string HtmlUrl { get; init; }
    public string Name { get; init; } = "";
    public DateTimeOffset? PublishedAt { get; init; }
    public string Body { get; init; } = "";
}
