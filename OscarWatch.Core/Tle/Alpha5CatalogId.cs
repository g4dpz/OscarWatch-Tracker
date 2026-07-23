using System.Globalization;

namespace OscarWatch.Core.Tle;

/// <summary>
/// Space-Track Alpha-5 catalogue numbers for fixed-width TLE satnum fields.
/// IDs below 100000 stay numeric (D5); 100000–339999 use a letter first digit (A=10 … Z=33, skipping I/O).
/// </summary>
public static class Alpha5CatalogId
{
    public const int MinAlpha5Value = 100_000;
    public const int MaxCatalogueValue = 339_999;

    /// <summary>Letters for high digits 10–33 (I and O omitted).</summary>
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static bool TryEncode(int noradCatId, out string field5)
    {
        field5 = "";
        if (noradCatId is < 0 or > MaxCatalogueValue)
            return false;

        if (noradCatId < MinAlpha5Value)
        {
            field5 = noradCatId.ToString("D5", CultureInfo.InvariantCulture);
            return true;
        }

        var high = noradCatId / 10_000;
        var low = noradCatId % 10_000;
        var letterIndex = high - 10;
        if (letterIndex < 0 || letterIndex >= Alphabet.Length)
            return false;

        field5 = $"{Alphabet[letterIndex]}{low.ToString("D4", CultureInfo.InvariantCulture)}";
        return true;
    }

    public static bool TryDecode(string? field, out int noradCatId)
    {
        noradCatId = 0;
        if (string.IsNullOrWhiteSpace(field))
            return false;

        var trimmed = field.Trim();
        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric))
        {
            if (numeric is < 0 or > MaxCatalogueValue)
                return false;

            noradCatId = numeric;
            return true;
        }

        if (trimmed.Length != 5)
            return false;

        var letter = char.ToUpperInvariant(trimmed[0]);
        var letterIndex = Alphabet.IndexOf(letter);
        if (letterIndex < 0)
            return false;

        if (!int.TryParse(trimmed.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var low)
            || low is < 0 or > 9999)
            return false;

        noradCatId = (letterIndex + 10) * 10_000 + low;
        return noradCatId <= MaxCatalogueValue;
    }

    public static bool IsAlpha5(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return false;

        var trimmed = field.Trim();
        if (trimmed.Length != 5)
            return false;

        var letter = char.ToUpperInvariant(trimmed[0]);
        return Alphabet.Contains(letter)
               && int.TryParse(trimmed.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    public static bool IsSupportedCatalogueId(string? field) => TryDecode(field, out _);

    /// <summary>Canonical 5-character TLE satnum (D5 or Alpha-5).</summary>
    public static string? Normalize(string? field) =>
        TryDecode(field, out var value) && TryEncode(value, out var encoded) ? encoded : null;
}
