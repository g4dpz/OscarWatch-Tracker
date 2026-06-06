using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;

namespace OscarWatch.Cloudlog;

public sealed class CloudlogRadioClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<(bool Ok, string? Error)> PostRadioAsync(
        CloudlogSettings settings,
        CloudlogRadioUpdate update,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildRadioEndpoint(settings.BaseUrl);
        if (endpoint is null)
            return (false, "Cloudlog URL is not configured.");

        var apiKey = settings.ApiKey.Trim();
        if (string.IsNullOrEmpty(apiKey))
            return (false, "API key is empty — enter your Cloudlog read/write API key in Settings.");

        var request = CloudlogRadioMapper.ToApiRequest(update, settings);
        request.Key = apiKey;

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
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

                return (false, CloudlogApiErrorHelper.DescribeFailure((int)response.StatusCode, body, apiKey.Length));
            }

            if (response.IsSuccessStatusCode)
                return (true, null);

            return (false, CloudlogApiErrorHelper.DescribeFailure((int)response.StatusCode, body, apiKey.Length));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static string? BuildRadioEndpoint(string? baseUrl) =>
        CloudlogApiEndpoints.BuildRadioEndpoint(baseUrl);
}
