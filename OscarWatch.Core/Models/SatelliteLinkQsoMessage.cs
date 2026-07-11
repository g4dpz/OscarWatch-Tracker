using System.Text.Json.Serialization;

namespace OscarWatch.Core.Models;

public enum SatelliteLinkQsoEventKind
{
    Logged,
    Updated,
    Deleted
}

/// <summary>WebSocket broadcast payload when a QSO is logged, updated, or deleted (protocol version 1).</summary>
public sealed class SatelliteLinkQsoMessage
{
    public const string LoggedType = "qsoLogged";
    public const string UpdatedType = "qsoUpdated";
    public const string DeletedType = "qsoDeleted";
    public const int ProtocolVersion = 1;

    [JsonPropertyName("type")]
    public string Type { get; init; } = LoggedType;

    [JsonPropertyName("version")]
    public int Version { get; init; } = ProtocolVersion;

    [JsonPropertyName("timestampUtc")]
    public string TimestampUtc { get; init; } = "";

    [JsonPropertyName("logbook")]
    public SatelliteLinkQsoLogbookInfo? Logbook { get; init; }

    [JsonPropertyName("qso")]
    public SatelliteLinkQsoInfo? Qso { get; init; }

    public static string MapType(SatelliteLinkQsoEventKind kind) => kind switch
    {
        SatelliteLinkQsoEventKind.Updated => UpdatedType,
        SatelliteLinkQsoEventKind.Deleted => DeletedType,
        _ => LoggedType
    };
}

public sealed class SatelliteLinkQsoLogbookInfo
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("myCallsign")]
    public string MyCallsign { get; init; } = "";

    [JsonPropertyName("myGridSquare")]
    public string MyGridSquare { get; init; } = "";
}

public sealed class SatelliteLinkQsoInfo
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("qsoUtc")]
    public string? QsoUtc { get; init; }

    [JsonPropertyName("call")]
    public string Call { get; init; } = "";

    [JsonPropertyName("rstSent")]
    public string? RstSent { get; init; }

    [JsonPropertyName("rstRcvd")]
    public string? RstRcvd { get; init; }

    [JsonPropertyName("gridSquare")]
    public string? GridSquare { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("satellite")]
    public SatelliteLinkQsoSatelliteInfo? Satellite { get; init; }

    [JsonPropertyName("frequencies")]
    public SatelliteLinkQsoFrequencyInfo? Frequencies { get; init; }

    [JsonPropertyName("bands")]
    public SatelliteLinkBandInfo? Bands { get; init; }

    [JsonPropertyName("propMode")]
    public string? PropMode { get; init; }
}

public sealed class SatelliteLinkQsoSatelliteInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("noradId")]
    public string? NoradId { get; init; }
}

public sealed class SatelliteLinkQsoFrequencyInfo
{
    [JsonPropertyName("uplinkHz")]
    public long UplinkHz { get; init; }

    [JsonPropertyName("downlinkHz")]
    public long DownlinkHz { get; init; }

    [JsonPropertyName("uplinkMode")]
    public string UplinkMode { get; init; } = "";

    [JsonPropertyName("downlinkMode")]
    public string DownlinkMode { get; init; } = "";
}
