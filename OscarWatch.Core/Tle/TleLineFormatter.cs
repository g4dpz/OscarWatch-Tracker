using System.Globalization;

namespace OscarWatch.Core.Tle;

/// <summary>
/// Formats GP element fields as fixed-width NORAD TLE lines for SGP4 libraries.
/// </summary>
public static class TleLineFormatter
{
    public static (string Line1, string Line2) FormatLines(GpElementRecord record, DateTime epochUtc)
    {
        if (!Alpha5CatalogId.TryEncode(record.NoradCatId.GetValueOrDefault(), out var noradId))
            throw new ArgumentOutOfRangeException(
                nameof(record),
                record.NoradCatId,
                $"NORAD catalogue ID must be between 0 and {Alpha5CatalogId.MaxCatalogueValue} for fixed-width TLE lines.");
        var classification = string.IsNullOrWhiteSpace(record.ClassificationType)
            ? "U"
            : record.ClassificationType.Trim()[0].ToString();
        var intlDesignator = FormatInternationalDesignator(record.ObjectId);
        var epochField = FormatEpochField(epochUtc);
        var meanMotionDotHalf = FormatMeanMotionDotHalf(record.MeanMotionDot.GetValueOrDefault());
        var meanMotionDdot = FormatScientificField(record.MeanMotionDdot.GetValueOrDefault());
        var bstar = FormatScientificField(record.Bstar.GetValueOrDefault());
        var ephemerisType = Math.Clamp(record.EphemerisType.GetValueOrDefault(), 0, 9);
        var elementSetNo = Math.Clamp(record.ElementSetNo.GetValueOrDefault(), 0, 9999);

        var line1Body =
            $"1 {noradId}{classification} {intlDesignator} {epochField} {meanMotionDotHalf} {meanMotionDdot} {bstar} {ephemerisType} {elementSetNo:D4}";
        var line1 = PadAndChecksum(line1Body, 69);

        var eccentricityField = FormatEccentricity(record.Eccentricity.GetValueOrDefault());
        var revolutionNumber = Math.Clamp(record.RevAtEpoch.GetValueOrDefault(), 0, 99999);
        var line2Body =
            $"2 {noradId} {record.Inclination.GetValueOrDefault(),8:F4} {record.RaOfAscNode.GetValueOrDefault(),8:F4} {eccentricityField} {record.ArgOfPericenter.GetValueOrDefault(),8:F4} {record.MeanAnomaly.GetValueOrDefault(),8:F4} {record.MeanMotion.GetValueOrDefault(),11:F8}{revolutionNumber,5:D5}";
        var line2 = PadAndChecksum(line2Body, 69);

        return (line1, line2);
    }

    internal static string FormatInternationalDesignator(string? objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return "00000A  ";

        var trimmed = objectId.Trim();
        var dash = trimmed.IndexOf('-');
        if (dash <= 0 || dash >= trimmed.Length - 1)
            return trimmed.Length >= 8 ? trimmed[..8] : trimmed.PadRight(8);

        var yearPart = trimmed[..dash];
        var launchPart = trimmed[(dash + 1)..];
        if (yearPart.Length < 2)
            return trimmed.PadRight(8);

        var yy = yearPart.Length >= 4 ? yearPart[^2..] : yearPart.PadLeft(2, '0');
        return $"{yy}{launchPart}".PadRight(8)[..8];
    }

    internal static string FormatEpochField(DateTime epochUtc)
    {
        var utc = epochUtc.Kind == DateTimeKind.Utc
            ? epochUtc
            : DateTime.SpecifyKind(epochUtc, DateTimeKind.Utc);

        var year = utc.Year % 100;
        var epochDay = utc.DayOfYear + utc.TimeOfDay.TotalSeconds / 86400.0;
        var dayText = epochDay.ToString("000.00000000", CultureInfo.InvariantCulture);
        return $"{year:D2}{dayText}";
    }

    internal static string FormatMeanMotionDotHalf(double meanMotionDot)
    {
        var half = meanMotionDot / 2.0;
        var text = half.ToString("0.00000000", CultureInfo.InvariantCulture);
        if (text.Length > 10)
            text = text[..10];
        return text.PadLeft(10);
    }

    internal static string FormatScientificField(double value)
    {
        if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            return " 00000-0";

        var sign = value < 0 ? '-' : ' ';
        var magnitude = Math.Abs(value);
        var exponent = 0;

        while (magnitude < 0.1)
        {
            magnitude *= 10;
            exponent--;
        }

        while (magnitude >= 1.0)
        {
            magnitude /= 10;
            exponent++;
        }

        var mantissa = (int)Math.Round(magnitude * 100_000.0, MidpointRounding.AwayFromZero);
        if (mantissa >= 100_000)
        {
            mantissa = 10_000;
            exponent++;
        }

        return $"{sign}{mantissa:D5}{exponent:+0;-0;0}";
    }

    internal static string FormatEccentricity(double eccentricity)
    {
        var text = eccentricity.ToString("0.0000000", CultureInfo.InvariantCulture);
        var digits = text.StartsWith("0.", StringComparison.Ordinal) ? text[2..] : text.Replace(".", "", StringComparison.Ordinal);
        if (digits.Length > 7)
            digits = digits[..7];
        return digits.PadLeft(7, '0');
    }

    internal static string PadAndChecksum(string body, int totalLineLength)
    {
        var bodyLength = totalLineLength - 1;
        var padded = body.Length >= bodyLength
            ? body[..bodyLength]
            : body.PadRight(bodyLength);
        var checksum = ComputeChecksum(padded);
        return padded + checksum.ToString(CultureInfo.InvariantCulture);
    }

    internal static int ComputeChecksum(string lineWithoutChecksum)
    {
        var sum = 0;
        foreach (var ch in lineWithoutChecksum)
        {
            if (ch is >= '0' and <= '9')
                sum += ch - '0';
            else if (ch == '-')
                sum += 1;
        }

        return sum % 10;
    }
}
