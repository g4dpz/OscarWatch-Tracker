namespace OscarWatch.Core.Logbook;

/// <summary>
/// Maps OscarWatch / common satellite names to LoTW <c>SAT_NAME</c> values for ADIF export.
/// </summary>
public static class LotwSatelliteNameMapper
{
    // LoTW name => name commonly stored in logs (inverse lookup for export).
    private static readonly Dictionary<string, string> LotwToSource = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ARISS"] = "ISS",
        ["UKUBE1"] = "UKUBE-1",
        ["KEDR"] = "ARISSAT-1",
        ["TO-108"] = "CAS-6",
        ["TAURUS"] = "TAURUS-1",
        ["AISAT1"] = "AISAT-1",
        ["UVSQ"] = "UVSQ-SAT",
        ["CAS-3H"] = "LILACSAT-2",
        ["IO-117"] = "GREENCUBE",
        ["TEVEL1"] = "TEVEL-1",
        ["TEVEL2"] = "TEVEL-2",
        ["TEVEL3"] = "TEVEL-3",
        ["TEVEL4"] = "TEVEL-4",
        ["TEVEL5"] = "TEVEL-5",
        ["TEVEL6"] = "TEVEL-6",
        ["TEVEL7"] = "TEVEL-7",
        ["TEVEL8"] = "TEVEL-8",
        ["INSPR7"] = "INSPIRE-SAT 7",
        ["SONATE"] = "SONATE-2",
        ["AO-123"] = "ASRTU-1",
        ["TEV2-1"] = "TEVEL2-1",
        ["TEV2-2"] = "TEVEL2-2",
        ["TEV2-3"] = "TEVEL2-3",
        ["TEV2-4"] = "TEVEL2-4",
        ["TEV2-5"] = "TEVEL2-5",
        ["TEV2-6"] = "TEVEL2-6",
        ["TEV2-7"] = "TEVEL2-7",
        ["TEV2-8"] = "TEVEL2-8",
        ["TEV2-9"] = "TEVEL2-9",
    };

    private static readonly Dictionary<string, string> SourceToLotw =
        LotwToSource.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    public static string MapForExport(string? satelliteName, bool forLotw)
    {
        if (!forLotw || string.IsNullOrWhiteSpace(satelliteName))
            return satelliteName?.Trim() ?? "";

        var trimmed = satelliteName.Trim();
        return SourceToLotw.TryGetValue(trimmed, out var lotwName) ? lotwName : trimmed;
    }
}
