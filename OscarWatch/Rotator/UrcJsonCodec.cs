using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OscarWatch.Rotator;

/// <summary>
/// OZ9AAR URC TCP/JSON codec: POLL / GOTO requests and AZ/EL status parsing.
/// See https://www.moonbounce.dk/hamradio/rotorcontroller
/// </summary>
public static class UrcJsonCodec
{
    public const string PollRequest = "{\"POLL\"}";

    public static string BuildGotoRequest(double azimuthDeg, double elevationDeg)
    {
        var az = azimuthDeg.ToString("0.###", CultureInfo.InvariantCulture);
        var el = elevationDeg.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{{\"GOTO\":[{az},{el}]}}";
    }

    public static bool TryParsePosition(string json, out double azimuthDeg, out double elevationDeg)
    {
        azimuthDeg = 0;
        elevationDeg = 0;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryGetDoubleProperty(root, "AZ", out azimuthDeg)
                && !TryGetDoubleProperty(root, "az", out azimuthDeg))
                return false;

            if (!TryGetDoubleProperty(root, "EL", out elevationDeg)
                && !TryGetDoubleProperty(root, "el", out elevationDeg))
                return false;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>True when <paramref name="buffer"/> contains one complete top-level JSON object.</summary>
    public static bool TryExtractCompleteObject(StringBuilder buffer, out string json)
    {
        json = "";
        if (buffer.Length == 0)
            return false;

        var start = -1;
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = 0; i < buffer.Length; i++)
        {
            var c = buffer[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                    inString = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    if (depth == 0)
                        start = i;
                    depth++;
                    break;
                case '}':
                    if (depth <= 0)
                        break;
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        json = buffer.ToString(start, i - start + 1);
                        buffer.Remove(0, i + 1);
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool TryGetDoubleProperty(JsonElement root, string name, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var prop))
            return false;

        switch (prop.ValueKind)
        {
            case JsonValueKind.Number:
                return prop.TryGetDouble(out value);
            case JsonValueKind.String:
                return double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }
}
