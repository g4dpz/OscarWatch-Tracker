using System.Globalization;
using System.Text.Json;

namespace OscarWatch.Core.Geo;

/// <summary>Parses gpsd JSON watch stream (TPV and SKY messages).</summary>
public static class GpsdJsonParser
{
    public sealed class GpsFixData
    {
        public double? LatitudeDeg { get; init; }
        public double? LongitudeDeg { get; init; }
        public double? AltitudeMeters { get; init; }
        public DateTime? UtcTime { get; init; }
        public int? SatellitesInUse { get; init; }
        public int? Mode { get; init; }

        public bool HasValidPosition =>
            Mode is >= 2
            && LatitudeDeg is not null
            && LongitudeDeg is not null;
    }

    public static bool TryParseTpvLine(string line, out GpsFixData fix)
    {
        fix = new GpsFixData();
        if (!TryGetRoot(line, out var root, "TPV"))
            return false;

        return TryParseTpv(root, out fix);
    }

    public static bool TryParseSkyLine(string line, out int satellitesInUse)
    {
        satellitesInUse = 0;
        if (!TryGetRoot(line, out var root, "SKY"))
            return false;

        if (!root.TryGetProperty("satellites", out var satellites)
            || satellites.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var satellite in satellites.EnumerateArray())
        {
            if (satellite.TryGetProperty("used", out var usedProp)
                && usedProp.ValueKind == JsonValueKind.True)
                satellitesInUse++;
        }

        return true;
    }

    private static bool TryGetRoot(string line, out JsonElement root, string expectedClass)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
            return false;

        try
        {
            using var doc = JsonDocument.Parse(line);
            root = doc.RootElement.Clone();
            return root.TryGetProperty("class", out var classProp)
                && classProp.GetString() == expectedClass;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseTpv(JsonElement root, out GpsFixData fix)
    {
        fix = new GpsFixData();

        if (!TryReadDouble(root, "lat", out var lat)
            || !TryReadDouble(root, "lon", out var lon))
            return false;

        TryReadDouble(root, "alt", out var alt);
        if (!TryReadInt(root, "mode", out var mode))
            mode = null;

        DateTime? utc = null;
        if (root.TryGetProperty("time", out var timeProp)
            && timeProp.ValueKind == JsonValueKind.String
            && DateTime.TryParse(
                timeProp.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedUtc))
        {
            utc = parsedUtc;
        }

        fix = new GpsFixData
        {
            LatitudeDeg = lat,
            LongitudeDeg = lon,
            AltitudeMeters = alt,
            UtcTime = utc,
            Mode = mode
        };
        return true;
    }

    private static bool TryReadDouble(JsonElement root, string name, out double? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var prop))
            return true;

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetDouble(out var number) && IsFinite(number) =>
                Set(ref value, number),
            JsonValueKind.String when double.TryParse(
                prop.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
                && IsFinite(parsed) =>
                Set(ref value, parsed),
            _ => true
        };
    }

    private static bool TryReadInt(JsonElement root, string name, out int? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var prop))
            return true;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var number))
            value = number;

        return true;
    }

    private static bool Set(ref double? target, double value)
    {
        target = value;
        return true;
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
