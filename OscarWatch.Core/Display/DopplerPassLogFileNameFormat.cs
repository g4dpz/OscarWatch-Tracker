using System.Diagnostics;
using System.Text;

namespace OscarWatch.Core.Display;

public static class DopplerPassLogFileNameFormat
{
    /// <summary>Matches Serilog <c>retainedFileCountLimit</c> for oscarwatch-.log (14 daily files).</summary>
    public const int RetainedFileDays = 14;

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

    /// <summary>
    /// Deletes <c>*.csv</c> files in <paramref name="directory"/> whose last write time is older
    /// than <see cref="RetainedFileDays"/> (same retention window as app Serilog files).
    /// </summary>
    /// <returns>Number of files deleted.</returns>
    public static int PruneOlderThanRetention(
        string directory,
        DateTime? utcNow = null,
        string? protectPath = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return 0;

        var now = utcNow ?? DateTime.UtcNow;
        if (now.Kind == DateTimeKind.Unspecified)
            now = DateTime.SpecifyKind(now, DateTimeKind.Utc);
        else if (now.Kind == DateTimeKind.Local)
            now = now.ToUniversalTime();

        var cutoff = now.AddDays(-RetainedFileDays);
        var deleted = 0;
        string? protectFull = null;
        if (!string.IsNullOrWhiteSpace(protectPath))
        {
            try
            {
                protectFull = Path.GetFullPath(protectPath);
            }
            catch
            {
                protectFull = null;
            }
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.csv", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (protectFull is not null
                    && string.Equals(Path.GetFullPath(path), protectFull, StringComparison.OrdinalIgnoreCase))
                    continue;

                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (lastWrite >= cutoff)
                    continue;

                File.Delete(path);
                deleted++;
            }
            catch
            {
                // Skip locked or inaccessible files; next prune will retry.
            }
        }

        return deleted;
    }

    public static void OpenLogDirectory(string? configuredFolder)
    {
        var directory = ResolveLogDirectory(configuredFolder);
        Directory.CreateDirectory(directory);
        PruneOlderThanRetention(directory);
        if (OperatingSystem.IsWindows())
        {
            var psi = new ProcessStartInfo
            {
                FileName = "explorer",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(directory);
            Process.Start(psi);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            var psi = new ProcessStartInfo
            {
                FileName = "open",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(directory);
            Process.Start(psi);
            return;
        }

        var linux = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false
        };
        linux.ArgumentList.Add(directory);
        Process.Start(linux);
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
