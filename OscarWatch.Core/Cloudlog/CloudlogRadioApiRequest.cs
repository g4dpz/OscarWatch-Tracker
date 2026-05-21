using System.Text.Json.Serialization;

namespace OscarWatch.Core.Cloudlog;

/// <summary>JSON body for Cloudlog <c>POST /index.php/api/radio</c> (v2 satellite fields).</summary>
public sealed class CloudlogRadioApiRequest
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("radio")]
    public string Radio { get; set; } = "OscarWatch";

    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("frequency_rx")]
    public string FrequencyRx { get; set; } = "";

    [JsonPropertyName("mode_rx")]
    public string ModeRx { get; set; } = "";

    [JsonPropertyName("prop_mode")]
    public string PropMode { get; set; } = "SAT";

    [JsonPropertyName("sat_name")]
    public string SatName { get; set; } = "";
}
