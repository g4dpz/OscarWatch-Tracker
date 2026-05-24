using System.Text;

namespace OscarWatch.Core.Display;

public static class RecordingFileNameFormat
{
    public static string BuildFileName(string satelliteName, DateTime utcStart)
    {
        var safeName = SanitizeSatelliteName(satelliteName);
        var stamp = utcStart.ToString("yy-MM-dd-HH-mm");
        return $"{safeName}-{stamp}.wav";
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
            path = Path.Combine(directory, $"{nameWithoutExt}-{suffix}.wav");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(directory, $"{nameWithoutExt}-{Guid.NewGuid():N}.wav");
    }

    public static string GetDefaultOutputFolder() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OscarWatch",
            "recordings");

    public static string ResolveOutputFolder(string? configuredFolder) =>
        string.IsNullOrWhiteSpace(configuredFolder)
            ? GetDefaultOutputFolder()
            : configuredFolder.Trim();

    internal static string SanitizeSatelliteName(string satelliteName)
    {
        if (string.IsNullOrWhiteSpace(satelliteName))
            return "satellite";

        var lower = satelliteName.Trim().ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
                builder.Append(ch);
            else if (ch is ' ' or '.')
                builder.Append('-');
        }

        var sanitized = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "satellite" : sanitized;
    }
}
