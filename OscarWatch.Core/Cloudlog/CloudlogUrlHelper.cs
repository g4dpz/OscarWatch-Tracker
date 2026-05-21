namespace OscarWatch.Core.Cloudlog;

public static class CloudlogUrlHelper
{
    public static string NormalizeBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        var trimmed = url.Trim().TrimEnd('/');
        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        return trimmed;
    }
}
