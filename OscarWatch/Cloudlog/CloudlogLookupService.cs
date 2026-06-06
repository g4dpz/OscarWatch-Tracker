using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Cloudlog;

public sealed class CloudlogLookupService : ICloudlogLookupService
{
    public const string SatelliteGridCheckBand = "SAT";

    private static readonly TimeSpan GridCacheTtl = TimeSpan.FromMinutes(10);

    private readonly HttpClient _httpClient;
    private readonly object _cacheLock = new();
    private string? _cachedSlug;
    private readonly Dictionary<string, (bool Worked, DateTime CachedAtUtc)> _gridCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CloudlogLookupService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultClient();
    }

    public bool CanCheckGrids(CloudlogSettings settings) =>
        settings.Enabled
        && settings.CheckRoveGrids
        && !string.IsNullOrWhiteSpace(settings.BaseUrl)
        && !string.IsNullOrWhiteSpace(settings.ApiKey)
        && !string.IsNullOrWhiteSpace(settings.LogbookPublicSlug);

    public async Task<CloudlogLogbooksResult> FetchLogbooksAsync(
        CloudlogSettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = settings.ApiKey?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrEmpty(apiKey))
            return CloudlogLogbooksResult.Failed("Enter your Cloudlog URL and API key first.");

        var endpoint = CloudlogApiEndpoints.BuildLogbooksAccessibleEndpoint(settings.BaseUrl, apiKey);
        if (endpoint is null)
            return CloudlogLogbooksResult.Failed("Cloudlog URL is not configured.");

        try
        {
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return CloudlogLogbooksResult.Failed(
                    CloudlogApiErrorHelper.DescribeFailure((int)response.StatusCode, body, apiKey.Length));

            var payload = JsonSerializer.Deserialize<LogbooksResponseDto>(body, JsonOptions);
            if (payload?.Logbooks is null)
                return CloudlogLogbooksResult.Failed("Unexpected response from Cloudlog.");

            if (string.Equals(payload.Status, "failed", StringComparison.OrdinalIgnoreCase))
                return CloudlogLogbooksResult.Failed(payload.Reason ?? "Cloudlog rejected the request.");

            var logbooks = payload.Logbooks
                .Where(l => !string.IsNullOrWhiteSpace(l.PublicSlug))
                .Select(l => new CloudlogLogbookInfo
                {
                    LogbookId = l.LogbookId,
                    LogbookName = l.LogbookName?.Trim() ?? l.PublicSlug ?? "",
                    PublicSlug = l.PublicSlug!.Trim(),
                    AccessLevel = l.AccessLevel
                })
                .OrderBy(l => l.LogbookName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return CloudlogLogbooksResult.Success(logbooks);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CloudlogLogbooksResult.Failed("Request timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CloudlogLogbooksResult.Failed(ex.Message);
        }
    }

    public async Task<CloudlogGridCheckResult?> CheckGridWorkedAsync(
        CloudlogSettings settings,
        string grid,
        CancellationToken cancellationToken = default)
    {
        if (!CanCheckGrids(settings))
            return null;

        var normalizedGrid = grid.Trim().ToUpperInvariant();
        if (normalizedGrid.Length == 0)
            return null;

        var slug = settings.LogbookPublicSlug.Trim();
        if (TryGetCachedGrid(slug, normalizedGrid, out var worked))
            return new CloudlogGridCheckResult { Grid = normalizedGrid, IsWorked = worked };

        var endpoint = CloudlogApiEndpoints.BuildLogbookCheckGridEndpoint(settings.BaseUrl);
        if (endpoint is null)
            return null;

        var payload = JsonSerializer.Serialize(new GridCheckRequestDto
        {
            Key = settings.ApiKey.Trim(),
            LogbookPublicSlug = slug,
            Grid = normalizedGrid,
            Band = SatelliteGridCheckBand
        }, JsonOptions);

        try
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = JsonSerializer.Deserialize<GridCheckResponseDto>(body, JsonOptions);
            if (result is null || string.IsNullOrWhiteSpace(result.Result))
                return null;

            worked = string.Equals(result.Result, "Found", StringComparison.OrdinalIgnoreCase);
            StoreCachedGrid(slug, normalizedGrid, worked);
            return new CloudlogGridCheckResult
            {
                Grid = result.Gridsquare?.Trim().ToUpperInvariant() ?? normalizedGrid,
                IsWorked = worked
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private bool TryGetCachedGrid(string slug, string grid, out bool worked)
    {
        lock (_cacheLock)
        {
            if (_cachedSlug == slug
                && _gridCache.TryGetValue(grid, out var entry)
                && DateTime.UtcNow - entry.CachedAtUtc < GridCacheTtl)
            {
                worked = entry.Worked;
                return true;
            }
        }

        worked = false;
        return false;
    }

    private void StoreCachedGrid(string slug, string grid, bool worked)
    {
        lock (_cacheLock)
        {
            if (!string.Equals(_cachedSlug, slug, StringComparison.Ordinal))
            {
                _cachedSlug = slug;
                _gridCache.Clear();
            }

            _gridCache[grid] = (worked, DateTime.UtcNow);
        }
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OscarWatch", "1.0"));
        return client;
    }

    private sealed class LogbooksResponseDto
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("reason")]
        public string? Reason { get; init; }

        [JsonPropertyName("logbooks")]
        public List<LogbookDto>? Logbooks { get; init; }
    }

    private sealed class LogbookDto
    {
        [JsonPropertyName("logbook_id")]
        public int LogbookId { get; init; }

        [JsonPropertyName("logbook_name")]
        public string? LogbookName { get; init; }

        [JsonPropertyName("public_slug")]
        public string? PublicSlug { get; init; }

        [JsonPropertyName("access_level")]
        public string? AccessLevel { get; init; }
    }

    private sealed class GridCheckRequestDto
    {
        [JsonPropertyName("key")]
        public string Key { get; init; } = "";

        [JsonPropertyName("logbook_public_slug")]
        public string LogbookPublicSlug { get; init; } = "";

        [JsonPropertyName("grid")]
        public string Grid { get; init; } = "";

        [JsonPropertyName("band")]
        public string Band { get; init; } = "";
    }

    private sealed class GridCheckResponseDto
    {
        [JsonPropertyName("gridsquare")]
        public string? Gridsquare { get; init; }

        [JsonPropertyName("result")]
        public string? Result { get; init; }
    }
}
