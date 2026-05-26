using System.Text.Json;
using System.Text.Json.Serialization;
using OscarWatch.Core.Json;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class SatelliteDatabaseFile
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions() =>
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new FlexibleDoubleJsonConverter() }
        };

    public static List<SatelliteRadioEntry> Load(string path)
    {
        if (!File.Exists(path))
            return [];

        var json = File.ReadAllText(path);
        return ParseJson(json);
    }

    public static List<SatelliteRadioEntry> ParseJson(string json) =>
        JsonSerializer.Deserialize<List<SatelliteRadioEntry>>(json, Options) ?? [];

    public static void Save(string path, IEnumerable<SatelliteRadioEntry> entries)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var list = entries
            .Select(NormalizeEntry)
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var json = JsonSerializer.Serialize(list, Options);
        File.WriteAllText(path, json);
    }

    public static void CopyBundledToUser(string bundledPath, string userPath)
    {
        if (!File.Exists(bundledPath))
            throw new FileNotFoundException("Bundled satellite database not found.", bundledPath);

        var directory = Path.GetDirectoryName(userPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.Copy(bundledPath, userPath, overwrite: true);
    }

    public static SatelliteRadioEntry NormalizeEntry(SatelliteRadioEntry entry)
    {
        entry.Name = entry.Name.Trim();
        entry.Modes = entry.Modes
            .Where(m => !string.IsNullOrWhiteSpace(m.Type) || m.DownlinkKHz > 0 || m.UplinkKHz > 0)
            .ToList();

        foreach (var mode in entry.Modes)
        {
            mode.Type = mode.Type.Trim();
            mode.DownlinkMode = mode.DownlinkMode.Trim();
            mode.UplinkMode = mode.UplinkMode.Trim();
            mode.Doppler = string.IsNullOrWhiteSpace(mode.Doppler) ? "NOR" : mode.Doppler.Trim().ToUpperInvariant();
            if (mode.CtcssHz is <= 0)
                mode.CtcssHz = null;
            if (mode.CtcssArmHz is <= 0)
                mode.CtcssArmHz = null;
        }

        return entry;
    }

    public static string? ValidateEntries(IReadOnlyList<SatelliteRadioEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                return "Every satellite needs a name.";

            if (!seen.Add(entry.Name.Trim()))
                return $"Duplicate satellite name: {entry.Name}";

            if (entry.Modes.Count == 0)
                return $"{entry.Name} has no transponder modes.";
        }

        return null;
    }
}
