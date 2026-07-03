using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Net;

namespace OscarWatch.Cloudlog;

public sealed class CloudlogQsoClient
{
    private static readonly HttpClient Http = OscarWatchHttpClients.Create(TimeSpan.FromSeconds(20));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<(bool Ok, string? Error)> PostQsoAsync(
        string baseUrl,
        string apiKey,
        int stationProfileId,
        string adifRecord,
        CancellationToken cancellationToken = default)
    {
        var endpoint = CloudlogApiEndpoints.BuildQsoEndpoint(baseUrl);
        if (endpoint is null)
            return (false, "Cloudlog URL is not configured.");

        var trimmedKey = apiKey.Trim();
        if (string.IsNullOrEmpty(trimmedKey))
            return (false, "API key is empty.");

        if (stationProfileId <= 0)
            return (false, "Station profile is not selected.");

        var payload = JsonSerializer.Serialize(new QsoUploadRequestDto
        {
            Key = trimmedKey,
            StationProfileId = stationProfileId.ToString(),
            Type = "adif",
            String = adifRecord
        }, JsonOptions);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await Http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (CloudlogResponseParser.TryParse(body, out var success, out _))
            {
                if (success)
                    return (true, null);

                return (false, CloudlogApiErrorHelper.DescribeFailure((int)response.StatusCode, body, trimmedKey.Length));
            }

            if (response.IsSuccessStatusCode)
                return (true, null);

            return (false, CloudlogApiErrorHelper.DescribeFailure((int)response.StatusCode, body, trimmedKey.Length));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }

    private sealed class QsoUploadRequestDto
    {
        [JsonPropertyName("key")]
        public string Key { get; init; } = "";

        [JsonPropertyName("station_profile_id")]
        public string StationProfileId { get; init; } = "";

        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("string")]
        public string String { get; init; } = "";
    }
}
