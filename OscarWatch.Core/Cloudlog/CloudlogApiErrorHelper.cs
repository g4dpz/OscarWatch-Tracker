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

        if (LooksLikeHtml(body) || string.IsNullOrWhiteSpace(body))
        {
            if (TryGetFriendlyStatusMessage(statusCode, out var friendly))
                return friendly;
        }

        if (!string.IsNullOrWhiteSpace(body))
            return $"Cloudlog HTTP {statusCode}: {Trim(body)}";

        return $"Cloudlog HTTP {statusCode}.";
    }

    private static bool TryGetFriendlyStatusMessage(int statusCode, out string message)
    {
        message = statusCode switch
        {
            401 => "Cloudlog rejected the request (unauthorized). Check your API key in Settings.",
            403 => "Cloudlog rejected the request (forbidden). Check your API key in Settings.",
            404 => "Cloudlog API endpoint not found. Check your Cloudlog URL in Settings.",
            500 => "Cloudlog server error. Try again later.",
            502 => "Cloudlog is unreachable (bad gateway). Try again later.",
            503 => "Cloudlog is temporarily unavailable. Try again in a few minutes.",
            504 => "Cloudlog took too long to respond. Try again later.",
            _ => ""
        };

        return message.Length > 0;
    }

    private static bool LooksLikeHtml(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        var trimmed = body.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("<body", StringComparison.OrdinalIgnoreCase);
    }

    private static string Trim(string body) =>
        body.Length <= 120 ? body.Trim() : body.Trim()[..120] + "…";
}
