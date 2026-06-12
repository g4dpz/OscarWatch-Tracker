using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OscarWatch.Core.Net;
using System.Text.Json.Serialization;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class HamsAtRovesService : IHamsAtRovesService
{
    public const string UpcomingAlertsUrl = "https://hams.at/api/alerts/upcoming";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly object _cacheLock = new();
    private string? _cachedApiKey;
    private IReadOnlyList<HamsAtUpcomingAlert>? _cachedAlerts;
    private DateTime _cachedAtUtc;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HamsAtRovesService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultClient();
    }

    public async Task<HamsAtFetchResult> FetchUpcomingAsync(
        HamsAtSettings settings,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var apiKey = settings.ApiKey?.Trim() ?? "";
        if (string.IsNullOrEmpty(apiKey))
            return HamsAtFetchResult.Failed("API key is required.");

        if (!bypassCache && TryGetCached(apiKey, out var cached))
            return HamsAtFetchResult.Success(cached);

        try
        {
            var alerts = await FetchFromApiAsync(apiKey, cancellationToken).ConfigureAwait(false);
            StoreCache(apiKey, alerts);
            return HamsAtFetchResult.Success(alerts);
        }
        catch (HttpRequestException ex)
        {
            return HamsAtFetchResult.Failed(DescribeHttpFailure(ex));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HamsAtFetchResult.Failed("Request timed out.");
        }
        catch (JsonException)
        {
            return HamsAtFetchResult.Failed("Unexpected response from hams.at.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HamsAtFetchResult.Failed(ex.Message);
        }
    }

    public async Task<(bool Ok, string Message)> TestConnectionAsync(
        HamsAtSettings settings,
        CancellationToken cancellationToken = default)
    {
        var result = await FetchUpcomingAsync(settings, bypassCache: true, cancellationToken)
            .ConfigureAwait(false);
        if (result.Ok)
        {
            var workable = result.Alerts.Count(a => a.IsWorkable);
            return (true, $"{workable} workable alert(s) returned.");
        }

        return (false, result.ErrorMessage ?? "Connection failed.");
    }

    private bool TryGetCached(string apiKey, out IReadOnlyList<HamsAtUpcomingAlert> alerts)
    {
        lock (_cacheLock)
        {
            if (_cachedAlerts is not null
                && _cachedApiKey == apiKey
                && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
            {
                alerts = _cachedAlerts;
                return true;
            }
        }

        alerts = [];
        return false;
    }

    private void StoreCache(string apiKey, IReadOnlyList<HamsAtUpcomingAlert> alerts)
    {
        lock (_cacheLock)
        {
            _cachedApiKey = apiKey;
            _cachedAlerts = alerts;
            _cachedAtUtc = DateTime.UtcNow;
        }
    }

    private async Task<IReadOnlyList<HamsAtUpcomingAlert>> FetchFromApiAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UpcomingAlertsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            throw new HttpRequestException("Invalid API key.", null, response.StatusCode);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<HamsAtUpcomingResponseDto>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return payload?.Data?.Select(MapAlert).ToArray() ?? [];
    }

    private static HamsAtUpcomingAlert MapAlert(HamsAtUpcomingAlertDto dto) => new()
    {
        Id = dto.Id ?? "",
        Callsign = dto.Callsign ?? "",
        Comment = dto.Comment ?? "",
        Url = dto.Url ?? "",
        Mode = dto.Mode ?? "",
        AosUtc = ParseUtc(dto.AosAt),
        LosUtc = ParseUtc(dto.LosAt),
        Grids = dto.Grids ?? [],
        Mhz = dto.Mhz,
        IsWorkable = dto.IsWorkable,
        Satellite = dto.Satellite is null
            ? null
            : new HamsAtSatelliteInfo
            {
                Name = dto.Satellite.Name ?? "",
                Number = dto.Satellite.Number
            }
    };

    private static DateTime ParseUtc(string? value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc)
            ? utc.ToUniversalTime()
            : DateTime.MinValue;

    private static string DescribeHttpFailure(HttpRequestException ex) =>
        ex.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Invalid API key.",
            HttpStatusCode.TooManyRequests => "Rate limited by hams.at. Try again later.",
            _ => string.IsNullOrWhiteSpace(ex.Message) ? "Network error." : ex.Message
        };

    private static HttpClient CreateDefaultClient() =>
        OscarWatchHttpClients.Create(TimeSpan.FromSeconds(30));

    private sealed class HamsAtUpcomingResponseDto
    {
        [JsonPropertyName("data")]
        public List<HamsAtUpcomingAlertDto>? Data { get; init; }
    }

    private sealed class HamsAtUpcomingAlertDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("callsign")]
        public string? Callsign { get; init; }

        [JsonPropertyName("comment")]
        public string? Comment { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("mode")]
        public string? Mode { get; init; }

        [JsonPropertyName("aos_at")]
        public string? AosAt { get; init; }

        [JsonPropertyName("los_at")]
        public string? LosAt { get; init; }

        [JsonPropertyName("grids")]
        public List<string>? Grids { get; init; }

        [JsonPropertyName("mhz")]
        public double? Mhz { get; init; }

        [JsonPropertyName("is_workable")]
        public bool IsWorkable { get; init; }

        [JsonPropertyName("satellite")]
        public HamsAtSatelliteDto? Satellite { get; init; }
    }

    private sealed class HamsAtSatelliteDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("number")]
        public int Number { get; init; }
    }
}
