using System.Text.Json;

namespace OscarWatch.Core.Cloudlog;

public static class CloudlogResponseParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParse(string? body, out bool success, out string? reason)
    {
        success = false;
        reason = null;
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            var doc = JsonSerializer.Deserialize<CloudlogApiResponse>(body, Options);
            if (doc is null)
                return false;

            reason = doc.Reason?.Trim();
            success = IsSuccessStatus(doc.Status);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? TryParseReason(string? body) =>
        TryParse(body, out _, out var reason) ? reason : null;

    internal static bool IsSuccessStatus(string? status) =>
        string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "created", StringComparison.OrdinalIgnoreCase);
}
