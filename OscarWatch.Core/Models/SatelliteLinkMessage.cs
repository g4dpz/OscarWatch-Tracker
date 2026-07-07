using System.Text.Json.Serialization;

namespace OscarWatch.Core.Models;

/// <summary>WebSocket broadcast payload (protocol version 1).</summary>
public sealed class SatelliteLinkMessage
{
    public const string MessageType = "satelliteStatus";
    public const int ProtocolVersion = 1;
    public const string NoSatelliteWispDde = "** NO SATELLITE **";

    [JsonPropertyName("type")]
    public string Type { get; init; } = MessageType;

    [JsonPropertyName("version")]
    public int Version { get; init; } = ProtocolVersion;

    [JsonPropertyName("timestampUtc")]
    public string TimestampUtc { get; init; } = "";

    [JsonPropertyName("inRange")]
    public bool InRange { get; init; }

    [JsonPropertyName("satellite")]
    public SatelliteLinkSatelliteInfo? Satellite { get; init; }

    [JsonPropertyName("frequencies")]
    public SatelliteLinkFrequencyInfo? Frequencies { get; init; }

    [JsonPropertyName("bands")]
    public SatelliteLinkBandInfo? Bands { get; init; }

    [JsonPropertyName("tracking")]
    public SatelliteLinkTrackingInfo? Tracking { get; init; }

    [JsonPropertyName("dopplerStrategy")]
    public string? DopplerStrategy { get; init; }

    [JsonPropertyName("wispDde")]
    public string WispDde { get; init; } = NoSatelliteWispDde;

    public string Signature =>
        InRange && Satellite is not null && Frequencies is not null
            ? $"{Satellite.Name}|{Satellite.NoradId}|{Frequencies.UplinkHz}|{Frequencies.DownlinkHz}|{Frequencies.UplinkMode}|{Frequencies.DownlinkMode}|{Tracking?.AzimuthDeg}|{Tracking?.ElevationDeg}"
            : "empty";
}

public sealed class SatelliteLinkSatelliteInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("noradId")]
    public string NoradId { get; init; } = "";

    [JsonPropertyName("modeType")]
    public string ModeType { get; init; } = "";
}

public sealed class SatelliteLinkFrequencyInfo
{
    [JsonPropertyName("uplinkHz")]
    public long UplinkHz { get; init; }

    [JsonPropertyName("downlinkHz")]
    public long DownlinkHz { get; init; }

    [JsonPropertyName("uplinkMode")]
    public string UplinkMode { get; init; } = "";

    [JsonPropertyName("downlinkMode")]
    public string DownlinkMode { get; init; } = "";

    [JsonPropertyName("nominalUplinkKHz")]
    public double NominalUplinkKHz { get; init; }

    [JsonPropertyName("nominalDownlinkKHz")]
    public double NominalDownlinkKHz { get; init; }

    [JsonPropertyName("isBeaconOnly")]
    public bool IsBeaconOnly { get; init; }
}

public sealed class SatelliteLinkBandInfo
{
    [JsonPropertyName("tx")]
    public string Tx { get; init; } = "";

    [JsonPropertyName("rx")]
    public string Rx { get; init; } = "";
}

public sealed class SatelliteLinkTrackingInfo
{
    [JsonPropertyName("azimuthDeg")]
    public double AzimuthDeg { get; init; }

    [JsonPropertyName("elevationDeg")]
    public double ElevationDeg { get; init; }

    [JsonPropertyName("rangeKm")]
    public double RangeKm { get; init; }

    [JsonPropertyName("rangeRateKmPerSec")]
    public double RangeRateKmPerSec { get; init; }

    [JsonPropertyName("isSunlit")]
    public bool IsSunlit { get; init; }
}
