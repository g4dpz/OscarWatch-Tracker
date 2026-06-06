namespace OscarWatch.Core.Cloudlog;

public static class CloudlogApiEndpoints
{
    public static string? BuildRadioEndpoint(string? baseUrl) =>
        TryBuild(baseUrl, "radio");

    public static string? BuildLogbooksAccessibleEndpoint(string? baseUrl, string apiKey) =>
        TryBuild(baseUrl, $"logbook_public_slugs_accessible/{Uri.EscapeDataString(apiKey.Trim())}");

    public static string? BuildLogbookCheckGridEndpoint(string? baseUrl) =>
        TryBuild(baseUrl, "logbook_check_grid");

    private static string? TryBuild(string? baseUrl, string path)
    {
        var normalized = CloudlogUrlHelper.NormalizeBaseUrl(baseUrl);
        if (string.IsNullOrEmpty(normalized))
            return null;

        return $"{normalized}/index.php/api/{path}";
    }
}
