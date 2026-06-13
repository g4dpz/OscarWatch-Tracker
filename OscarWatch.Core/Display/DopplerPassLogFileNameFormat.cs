using System.Diagnostics;
using System.Text;

namespace OscarWatch.Core.Display;

public static class DopplerPassLogFileNameFormat
{
    public static string GetDefaultLogDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OscarWatch",
            "doppler-logs");

    public static string ResolveLogDirectory(string? configuredFolder) =>
        string.IsNullOrWhiteSpace(configuredFolder)
            ? GetDefaultLogDirectory()
            : configuredFolder.Trim();

    public static string BuildFileName(string satelliteName, DateTime utcStart)
    {
        var safeName = SanitizeSatelliteName(satelliteName);
        var stamp = utcStart.ToString("yy-MM-dd-HH-mm");
        return $"{safeName}-{stamp}-doppler.csv";
    }

    public static string ResolveUniquePath(string directory, string satelliteName, DateTime utcStart)
    {
        Directory.CreateDirectory(directory);
        var baseName = BuildFileName(satelliteName, utcStart);
        var path = Path.Combine(directory, baseName);
        if (!File.Exists(path))
            return path;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
        for (var suffix = 2; suffix < 1000; suffix++)
        {
            path = Path.Combine(directory, $"{nameWithoutExt}-{suffix}.csv");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(directory, $"{nameWithoutExt}-{Guid.NewGuid():N}.csv");
    }

    public static void OpenLogDirectory(string? configuredFolder)
    {
        var directory = ResolveLogDirectory(configuredFolder);
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    private static string SanitizeSatelliteName(string satelliteName)
    {
        var trimmed = satelliteName.Trim();
        if (trimmed.Length == 0)
            return "satellite";

        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
                builder.Append(ch);
            else if (char.IsWhiteSpace(ch))
                builder.Append('-');
        }

        var safe = builder.ToString().Trim('-');
        return safe.Length == 0 ? "satellite" : safe;
    }
}
