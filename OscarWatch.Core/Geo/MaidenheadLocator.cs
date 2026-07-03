using System.Text.RegularExpressions;

namespace OscarWatch.Core.Geo;

public enum GridValidationError
{
    None,
    TooManyGrids,
    InvalidSegment
}

/// <summary>Maidenhead locator normalisation and validation for logging (2/4/6/8 characters, multi-grid).</summary>
public static partial class MaidenheadLocator
{
    /// <summary>Maximum grids stored together (e.g. four 6-character squares at a corner).</summary>
    public const int MaxGridCount = 4;

    public static string NormalizeCallsign(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToUpperInvariant();

    /// <summary>Uppercase grid entry text without re-splitting or de-duping (for live input).</summary>
    public static string UppercaseGridEntry(string? value) =>
        string.IsNullOrEmpty(value) ? "" : value.ToUpperInvariant();

    /// <summary>Uppercase, de-dupe, and comma-join multiple locators from mixed separators.</summary>
    public static string NormalizeGrids(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var segments = SplitGridInput(value);
        if (segments.Count == 0)
            return "";

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>(segments.Count);
        foreach (var segment in segments)
        {
            var normalized = segment.Trim().ToUpperInvariant();
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;

            ordered.Add(normalized);
        }

        return string.Join(',', ordered);
    }

    public static bool TryValidateGrids(
        string? value,
        out string normalized,
        out GridValidationError error,
        out string? invalidSegment)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = "";
            error = GridValidationError.None;
            invalidSegment = null;
            return true;
        }

        var segments = SplitGridInput(value);
        if (segments.Count > MaxGridCount)
        {
            normalized = "";
            error = GridValidationError.TooManyGrids;
            invalidSegment = null;
            return false;
        }

        normalized = NormalizeGrids(value);
        if (string.IsNullOrEmpty(normalized))
        {
            error = GridValidationError.None;
            invalidSegment = null;
            return true;
        }

        foreach (var segment in normalized.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsValidSegment(segment))
            {
                normalized = "";
                error = GridValidationError.InvalidSegment;
                invalidSegment = segment;
                return false;
            }
        }

        error = GridValidationError.None;
        invalidSegment = null;
        return true;
    }

    public static bool IsValidSegment(string segment)
    {
        if (segment.Length is not (2 or 4 or 6 or 8))
            return false;

        return GridSegmentPattern().IsMatch(segment);
    }

    /// <summary>Null when empty; otherwise whether the current entry passes grid validation.</summary>
    public static bool? GetLiveValidationState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return TryValidateGrids(value, out _, out _, out _);
    }

    private static IReadOnlyList<string> SplitGridInput(string value) =>
        value.Split([',', ';', '/', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [GeneratedRegex("^[A-R]{2}([0-9]{2}([A-X]{2}([0-9]{2})?)?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex GridSegmentPattern();
}
