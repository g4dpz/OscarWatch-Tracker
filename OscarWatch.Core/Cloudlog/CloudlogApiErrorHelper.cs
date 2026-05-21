namespace OscarWatch.Core.Cloudlog;

public static class CloudlogApiErrorHelper
{
    public static string DescribeFailure(int statusCode, string? body, int apiKeyLength)
    {
        var parsed = CloudlogResponseParser.TryParseReason(body);
        if (parsed is not null && parsed.Contains("missing api key", StringComparison.OrdinalIgnoreCase))
        {
            if (apiKeyLength == 0)
                return "API key is empty in OscarWatch — re-enter it in Settings → Cloudlog and Save.";

            return "Cloudlog rejected the API key. In Cloudlog go to Admin → API Keys, create a read/write key, copy it exactly (no spaces), and paste it here. "
                   + "Note: Cloudlog returns \"missing api key\" for invalid or disabled keys too.";
        }

        if (!string.IsNullOrWhiteSpace(parsed))
            return $"Cloudlog HTTP {statusCode}: {parsed}";

        if (!string.IsNullOrWhiteSpace(body))
            return $"Cloudlog HTTP {statusCode}: {Trim(body)}";

        return $"Cloudlog HTTP {statusCode}.";
    }

    private static string Trim(string body) =>
        body.Length <= 240 ? body.Trim() : body.Trim()[..240] + "…";
}
