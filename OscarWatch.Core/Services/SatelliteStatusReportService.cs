using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OscarWatch.Core.Models;
using OscarWatch.Core.Net;

namespace OscarWatch.Core.Services;

public sealed class SatelliteStatusReportService : ISatelliteStatusReportService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SatelliteStatusReportService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultClient();
    }

    public async Task<SatelliteStatusTokenTestResult> TestTokenAsync(
        SatelliteStatusSettings settings,
        CancellationToken cancellationToken = default)
    {
        var token = settings.ApiToken?.Trim() ?? "";
        if (string.IsNullOrEmpty(token))
            return new SatelliteStatusTokenTestResult(false, "API token is required.", 0);

        if (!TryBuildUri(settings.BaseUrl, "/api/v1/me", out var uri, out var urlError))
            return new SatelliteStatusTokenTestResult(false, urlError, 0);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
                return new SatelliteStatusTokenTestResult(true, Truncate(body, 120), code);

            return new SatelliteStatusTokenTestResult(
                false,
                FormatError(response.StatusCode, body),
                code);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return new SatelliteStatusTokenTestResult(false, ex.Message, 0);
        }
    }

    public async Task<SatelliteStatusReportResult> SubmitReportAsync(
        SatelliteStatusSettings settings,
        SatelliteStatusReportRequest report,
        CancellationToken cancellationToken = default)
    {
        var token = settings.ApiToken?.Trim() ?? "";
        if (string.IsNullOrEmpty(token))
            return new SatelliteStatusReportResult(false, false, "API token is required.", 0);

        if (!TryBuildUri(settings.BaseUrl, "/api/v1/satellite-status/reports", out var uri, out var urlError))
            return new SatelliteStatusReportResult(false, false, urlError, 0);

        var payload = new ReportBody
        {
            Satellite = report.Satellite.Trim(),
            Mode = report.Mode.Trim(),
            Status = ToApiStatus(report.Status),
            ObservedAt = report.ObservedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            Gridsquare = string.IsNullOrWhiteSpace(report.Gridsquare) ? null : report.Gridsquare.Trim().ToUpperInvariant(),
            Client = string.IsNullOrWhiteSpace(report.Client) ? null : report.Client.Trim()
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var code = (int)response.StatusCode;

            if (response.StatusCode == HttpStatusCode.Created)
                return new SatelliteStatusReportResult(true, true, "Report stored.", code);

            if (response.StatusCode == HttpStatusCode.OK)
                return new SatelliteStatusReportResult(true, false, "Duplicate report (already stored recently).", code);

            return new SatelliteStatusReportResult(
                false,
                false,
                FormatError(response.StatusCode, body),
                code);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return new SatelliteStatusReportResult(false, false, ex.Message, 0);
        }
    }

    public async Task<SatelliteStatusFetchResult> FetchCommunityAsync(
        SatelliteStatusSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildUri(settings.BaseUrl, "/api/v1/satellite-status", out var uri, out var urlError))
            return new SatelliteStatusFetchResult(false, false, null, urlError, 0);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var code = (int)response.StatusCode;

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new SatelliteStatusFetchResult(false, true, null, FormatError(response.StatusCode, body), code);

            if (!response.IsSuccessStatusCode)
                return new SatelliteStatusFetchResult(false, false, null, FormatError(response.StatusCode, body), code);

            var catalog = ParseCommunityCatalog(body, DateTime.UtcNow);
            if (catalog is null)
                return new SatelliteStatusFetchResult(false, false, null, "Invalid community status response.", code);

            return new SatelliteStatusFetchResult(true, false, catalog, "OK", code);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            return new SatelliteStatusFetchResult(false, false, null, ex.Message, 0);
        }
    }

    internal static SatelliteCommunityCatalog? ParseCommunityCatalog(string body, DateTime fetchedAtUtc)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var windowHours = root.TryGetProperty("window_hours", out var wh) && wh.TryGetInt32(out var whInt)
            ? whInt
            : 24;

        var serverTime = DateTime.UtcNow;
        if (root.TryGetProperty("server_time_utc", out var st) && st.ValueKind == JsonValueKind.String
            && DateTime.TryParse(st.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedServer))
        {
            serverTime = parsedServer.ToUniversalTime();
        }

        var satellites = new List<SatelliteCommunitySatelliteStatus>();
        if (root.TryGetProperty("satellites", out var sats) && sats.ValueKind == JsonValueKind.Array)
        {
            foreach (var satEl in sats.EnumerateArray())
            {
                var name = satEl.TryGetProperty("name", out var n) ? n.GetString()?.Trim() ?? "" : "";
                if (name.Length == 0)
                    continue;

                var modes = new List<SatelliteCommunityModeStatus>();
                if (satEl.TryGetProperty("modes", out var modesEl) && modesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var modeEl in modesEl.EnumerateArray())
                    {
                        var type = modeEl.TryGetProperty("type", out var t) ? t.GetString()?.Trim() ?? "" : "";
                        if (type.Length == 0)
                            continue;

                        string? statusRaw = null;
                        if (modeEl.TryGetProperty("status", out var statusEl)
                            && statusEl.ValueKind == JsonValueKind.String)
                        {
                            statusRaw = statusEl.GetString();
                        }

                        var kind = SatelliteStatusReportFormatting.ParseCommunityStatus(statusRaw);
                        var label = modeEl.TryGetProperty("status_label", out var sl)
                                    && sl.ValueKind == JsonValueKind.String
                            ? sl.GetString()
                            : null;
                        var reportCount = modeEl.TryGetProperty("report_count", out var rc) && rc.TryGetInt32(out var rci)
                            ? rci
                            : 0;

                        var recent = new List<SatelliteCommunityRecentReport>();
                        DateTime? newest = null;
                        if (modeEl.TryGetProperty("recent_reports", out var rr) && rr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var reportEl in rr.EnumerateArray())
                            {
                                var call = reportEl.TryGetProperty("callsign", out var c) ? c.GetString() ?? "" : "";
                                var grid = reportEl.TryGetProperty("gridsquare", out var g) ? g.GetString() ?? "" : "";
                                var rStatus = reportEl.TryGetProperty("status", out var rs) && rs.ValueKind == JsonValueKind.String
                                    ? rs.GetString()
                                    : null;
                                var rKind = SatelliteStatusReportFormatting.ParseCommunityStatus(rStatus);
                                DateTime observed = default;
                                if (reportEl.TryGetProperty("observed_at", out var oa)
                                    && oa.ValueKind == JsonValueKind.String
                                    && DateTime.TryParse(
                                        oa.GetString(),
                                        null,
                                        System.Globalization.DateTimeStyles.RoundtripKind,
                                        out var observedParsed))
                                {
                                    observed = observedParsed.ToUniversalTime();
                                    if (newest is null || observed > newest)
                                        newest = observed;
                                }

                                recent.Add(new SatelliteCommunityRecentReport(call, grid, rKind, observed));
                            }
                        }

                        modes.Add(new SatelliteCommunityModeStatus(
                            type,
                            kind,
                            label,
                            reportCount,
                            newest,
                            recent));
                    }
                }

                satellites.Add(new SatelliteCommunitySatelliteStatus(name, modes));
            }
        }

        return new SatelliteCommunityCatalog(satellites, windowHours, serverTime, fetchedAtUtc.ToUniversalTime());
    }

    private static string ToApiStatus(SatelliteStatusValue status) => status switch
    {
        SatelliteStatusValue.On => "on",
        SatelliteStatusValue.Off => "off",
        SatelliteStatusValue.TelemetryOnly => "telemetry_only",
        _ => "on"
    };

    private static bool TryBuildUri(string? baseUrl, string path, out Uri uri, out string error)
    {
        uri = null!;
        error = "";
        var root = (baseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(root))
        {
            error = "Base URL is required.";
            return false;
        }

        if (!Uri.TryCreate(root + path, UriKind.Absolute, out var built) ||
            (built.Scheme != Uri.UriSchemeHttps && built.Scheme != Uri.UriSchemeHttp))
        {
            error = "Base URL is not a valid http(s) address.";
            return false;
        }

        uri = built;
        return true;
    }

    private static string FormatError(HttpStatusCode status, string body)
    {
        var detail = TryExtractMessage(body);
        var prefix = status switch
        {
            HttpStatusCode.Unauthorized => "Unauthorised",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not found (feature may be inactive)",
            HttpStatusCode.UnprocessableEntity => "Validation failed",
            _ => $"HTTP {(int)status}"
        };

        return string.IsNullOrWhiteSpace(detail) ? prefix : $"{prefix}: {detail}";
    }

    private static string TryExtractMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                return message.GetString() ?? "";

            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                    {
                        var first = prop.Value[0];
                        if (first.ValueKind == JsonValueKind.String)
                            return first.GetString() ?? "";
                    }
                }
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return Truncate(body, 160);
    }

    private static string Truncate(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max)
            return text;
        return text[..max] + "…";
    }

    private static HttpClient CreateDefaultClient() =>
        OscarWatchHttpClients.Create(TimeSpan.FromSeconds(30));

    private sealed class ReportBody
    {
        public string Satellite { get; set; } = "";
        public string Mode { get; set; } = "";
        public string Status { get; set; } = "";
        public string ObservedAt { get; set; } = "";
        public string? Gridsquare { get; set; }
        public string? Client { get; set; }
    }
}
