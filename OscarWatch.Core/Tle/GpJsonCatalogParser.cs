using System.Globalization;
using System.Text.Json;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Tle;

public static class GpJsonCatalogParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public static IReadOnlyList<SatelliteCatalogEntry> ParseCatalog(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var trimmed = json.TrimStart();
        List<GpElementRecord> records = trimmed.StartsWith("[", StringComparison.Ordinal)
            ? JsonSerializer.Deserialize<List<GpElementRecord>>(trimmed, JsonOptions) ?? []
            : JsonSerializer.Deserialize<GpElementRecord>(trimmed, JsonOptions) is { } single
                ? [single]
                : [];

        var entries = new List<SatelliteCatalogEntry>(records.Count);
        foreach (var record in records)
        {
            if (TryMapRecord(record, out var entry))
                entries.Add(entry);
        }

        return entries;
    }

    private static bool TryMapRecord(GpElementRecord record, out SatelliteCatalogEntry entry)
    {
        entry = null!;
        var name = ResolveName(record);
        if (string.IsNullOrWhiteSpace(name) || record.NoradCatId <= 0)
            return false;

        if (!TryParseEpoch(record.Epoch, out var epochUtc))
            return false;

        var (line1, line2) = TleLineFormatter.FormatLines(record, epochUtc);
        entry = new SatelliteCatalogEntry
        {
            Name = name,
            NoradId = record.NoradCatId.ToString(CultureInfo.InvariantCulture),
            Line1 = line1,
            Line2 = line2,
            EpochUtc = epochUtc
        };
        return true;
    }

    internal static string? ResolveName(GpElementRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.AmsatName))
            return record.AmsatName.Trim();

        if (!string.IsNullOrWhiteSpace(record.ObjectName))
            return record.ObjectName.Trim();

        return null;
    }

    internal static bool TryParseEpoch(string? epoch, out DateTime epochUtc)
    {
        epochUtc = default;
        if (string.IsNullOrWhiteSpace(epoch))
            return false;

        if (DateTime.TryParse(epoch, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out epochUtc))
            return true;

        return DateTime.TryParse(epoch, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out epochUtc)
               && epochUtc.Kind == DateTimeKind.Utc;
    }
}
