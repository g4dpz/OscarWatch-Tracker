using System.Text.Json.Serialization;

namespace OscarWatch.Core.Tle;

/// <summary>
/// GP element set in AMSAT / OscarWatch JSON catalogue format.
/// </summary>
public sealed class GpElementRecord
{
    [JsonPropertyName("AMSAT_NAME")]
    public string? AmsatName { get; set; }

    [JsonPropertyName("OBJECT_NAME")]
    public string? ObjectName { get; set; }

    [JsonPropertyName("OBJECT_ID")]
    public string? ObjectId { get; set; }

    [JsonPropertyName("NORAD_CAT_ID")]
    public int? NoradCatId { get; set; }

    [JsonPropertyName("EPOCH")]
    public string? Epoch { get; set; }

    [JsonPropertyName("INCLINATION")]
    public double? Inclination { get; set; }

    [JsonPropertyName("ECCENTRICITY")]
    public double? Eccentricity { get; set; }

    [JsonPropertyName("RA_OF_ASC_NODE")]
    public double? RaOfAscNode { get; set; }

    [JsonPropertyName("ARG_OF_PERICENTER")]
    public double? ArgOfPericenter { get; set; }

    [JsonPropertyName("MEAN_ANOMALY")]
    public double? MeanAnomaly { get; set; }

    [JsonPropertyName("MEAN_MOTION")]
    public double? MeanMotion { get; set; }

    [JsonPropertyName("MEAN_MOTION_DOT")]
    public double? MeanMotionDot { get; set; }

    [JsonPropertyName("MEAN_MOTION_DDOT")]
    public double? MeanMotionDdot { get; set; }

    [JsonPropertyName("BSTAR")]
    public double? Bstar { get; set; }

    [JsonPropertyName("EPHEMERIS_TYPE")]
    public int? EphemerisType { get; set; }

    [JsonPropertyName("CLASSIFICATION_TYPE")]
    public string? ClassificationType { get; set; }

    [JsonPropertyName("ELEMENT_SET_NO")]
    public int? ElementSetNo { get; set; }

    [JsonPropertyName("REV_AT_EPOCH")]
    public int? RevAtEpoch { get; set; }
}
