using System.Text.Json.Serialization;

namespace OscarWatch.Core.Models;

public sealed class SatelliteRadioEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("modes")]
    public List<SatelliteTransponderMode> Modes { get; set; } = [];
}
