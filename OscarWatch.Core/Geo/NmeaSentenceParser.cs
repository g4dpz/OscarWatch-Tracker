using System.Globalization;

namespace OscarWatch.Core.Geo;

/// <summary>Parses common NMEA 0183 position/time sentences (GGA, RMC).</summary>
public static class NmeaSentenceParser
{
    public sealed class GpsFixData
    {
        public double? LatitudeDeg { get; init; }
        public double? LongitudeDeg { get; init; }
        public double? AltitudeMeters { get; init; }
        public DateTime? UtcTime { get; init; }
        public int? SatellitesInUse { get; init; }
        public int? FixQuality { get; init; }

        public bool HasValidPosition =>
            LatitudeDeg is not null
            && LongitudeDeg is not null
            && FixQuality is > 0;
    }

    public static bool TryParseGga(string sentence, out GpsFixData fix)
    {
        fix = new GpsFixData();
        if (!TrySplitSentence(sentence, "GGA", out var fields) || fields.Length < 10)
            return false;

        if (!TryParseLatitude(fields[2], fields[3], out var lat)
            || !TryParseLongitude(fields[4], fields[5], out var lon))
            return false;

        if (!int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quality))
            quality = 0;

        int? sats = int.TryParse(fields[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var satCount)
            ? satCount
            : null;

        double? alt = double.TryParse(fields[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var altitude)
            ? altitude
            : null;

        DateTime? utc = TryParseUtcTime(fields[1], date: null);

        fix = new GpsFixData
        {
            LatitudeDeg = lat,
            LongitudeDeg = lon,
            AltitudeMeters = alt,
            UtcTime = utc,
            SatellitesInUse = sats,
            FixQuality = quality
        };
        return true;
    }

    public static bool TryParseRmc(string sentence, out GpsFixData fix)
    {
        fix = new GpsFixData();
        if (!TrySplitSentence(sentence, "RMC", out var fields) || fields.Length < 10)
            return false;

        if (!string.Equals(fields[2], "A", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryParseLatitude(fields[3], fields[4], out var lat)
            || !TryParseLongitude(fields[5], fields[6], out var lon))
            return false;

        DateTime? utc = TryParseUtcTime(fields[1], fields[9]);

        fix = new GpsFixData
        {
            LatitudeDeg = lat,
            LongitudeDeg = lon,
            UtcTime = utc,
            FixQuality = 1
        };
        return true;
    }

    public static bool TryParseLine(string line, out GpsFixData fix)
    {
        fix = new GpsFixData();
        if (string.IsNullOrWhiteSpace(line) || line[0] != '$')
            return false;

        if (TryParseGga(line, out fix))
            return true;

        return TryParseRmc(line, out fix);
    }

    private static bool TrySplitSentence(string sentence, string sentenceType, out string[] fields)
    {
        fields = [];
        var star = sentence.IndexOf('*', StringComparison.Ordinal);
        var body = star >= 0 ? sentence[..star] : sentence;
        if (body.Length == 0 || body[0] != '$')
            return false;

        var parts = body[1..].Split(',');
        if (parts.Length < 2)
            return false;

        var type = parts[0];
        if (type.Length >= 3 && type[^3..] == sentenceType)
        {
            fields = parts;
            return true;
        }

        return false;
    }

    private static bool TryParseLatitude(string value, string hemisphere, out double latitude)
    {
        latitude = 0;
        return TryParseLatLon(value, hemisphere, isLatitude: true, out latitude);
    }

    private static bool TryParseLongitude(string value, string hemisphere, out double longitude)
    {
        longitude = 0;
        return TryParseLatLon(value, hemisphere, isLatitude: false, out longitude);
    }

    private static bool TryParseLatLon(string value, string hemisphere, bool isLatitude, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(hemisphere))
            return false;

        var dot = value.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
            return false;

        var degreeDigits = isLatitude ? 2 : 3;
        if (value.Length <= degreeDigits)
            return false;

        if (!int.TryParse(value[..degreeDigits], NumberStyles.Integer, CultureInfo.InvariantCulture, out var degrees))
            return false;

        if (!double.TryParse(value[degreeDigits..], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
            return false;

        result = degrees + minutes / 60.0;
        if (hemisphere.Equals("S", StringComparison.OrdinalIgnoreCase)
            || hemisphere.Equals("W", StringComparison.OrdinalIgnoreCase))
            result = -result;

        return true;
    }

    private static DateTime? TryParseUtcTime(string timeField, string? date)
    {
        if (string.IsNullOrWhiteSpace(timeField) || timeField.Length < 6)
            return null;

        if (!int.TryParse(timeField[..2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(timeField[2..4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute))
            return null;

        var secondToken = timeField[4..];
        var dot = secondToken.IndexOf('.', StringComparison.Ordinal);
        if (dot >= 0)
            secondToken = secondToken[..dot];

        if (!int.TryParse(secondToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var second))
            return null;

        int day = 1, month = 1, year = DateTime.UtcNow.Year;
        if (!string.IsNullOrWhiteSpace(date) && date.Length >= 6)
        {
            if (!int.TryParse(date[..2], NumberStyles.Integer, CultureInfo.InvariantCulture, out day)
                || !int.TryParse(date[2..4], NumberStyles.Integer, CultureInfo.InvariantCulture, out month)
                || !int.TryParse(date[4..6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var yy))
                return null;

            year = yy >= 80 ? 1900 + yy : 2000 + yy;
        }

        try
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }
}
