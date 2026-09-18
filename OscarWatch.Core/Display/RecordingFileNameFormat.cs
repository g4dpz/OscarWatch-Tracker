using System.Diagnostics;
using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Display;

public static class RecordingFileNameFormat
{
    public static string BuildFileName(
        string satelliteName,
        DateTime utcStart,
        RecordingContainerFormat container = RecordingContainerFormat.Wav)
    {
        var safeName = SanitizeSatelliteName(satelliteName);
        var stamp = utcStart.ToString("yy-MM-dd-HH-mm");
        return $"{safeName}-{stamp}{container.GetExtension()}";
    }

    public static string ResolveUniquePath(
        string directory,
        string satelliteName,
        DateTime utcStart,
        RecordingContainerFormat container = RecordingContainerFormat.Wav)
    {
        Directory.CreateDirectory(directory);
        var baseName = BuildFileName(satelliteName, utcStart, container);
        var path = Path.Combine(directory, baseName);
        if (!StemTaken(directory, Path.GetFileNameWithoutExtension(baseName)))
            return path;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidateStem = $"{nameWithoutExt}-{suffix}";
            if (!StemTaken(directory, candidateStem))
                return Path.Combine(directory, candidateStem + container.GetExtension());
        }

        return Path.Combine(directory, $"{nameWithoutExt}-{Guid.NewGuid():N}{container.GetExtension()}");
    }

    /// <summary>
    /// Capture always writes WAV; when the preferred container is MP3 this is the intermediate path.
    /// </summary>
    public static string GetCaptureWavPath(string preferredOutputPath) =>
        Path.ChangeExtension(preferredOutputPath, ".wav");

    public static string GetDefaultOutputFolder() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OscarWatch",
            "recordings");

    public static string ResolveOutputFolder(string? configuredFolder) =>
        string.IsNullOrWhiteSpace(configuredFolder)
            ? GetDefaultOutputFolder()
            : configuredFolder.Trim();

    /// <summary>
    /// Counts top-level <c>.wav</c> / <c>.mp3</c> files and total bytes in the recordings folder.
    /// </summary>
    public static (int FileCount, long TotalBytes, bool DirectoryExists) MeasureUsage(string? configuredFolder)
    {
        var folder = ResolveOutputFolder(configuredFolder);
        if (!Directory.Exists(folder))
            return (0, 0, false);

        var count = 0;
        long total = 0;
        foreach (var path in Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly))
        {
            var ext = Path.GetExtension(path);
            if (!ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                total += new FileInfo(path).Length;
                count++;
            }
            catch
            {
                // Skip inaccessible files.
            }
        }

        return (count, total, true);
    }

    /// <summary>Formats a byte count for operator-facing UI (e.g. 1.8 GB, 820 MB, 12 KB).</summary>
    public static string FormatByteSize(long bytes)
    {
        if (bytes < 0)
            bytes = 0;

        const double kb = 1024.0;
        const double mb = kb * 1024.0;
        const double gb = mb * 1024.0;

        if (bytes >= gb)
            return $"{bytes / gb:0.#} GB";
        if (bytes >= mb)
            return $"{bytes / mb:0.#} MB";
        if (bytes >= kb)
            return $"{bytes / kb:0.#} KB";
        return $"{bytes} B";
    }

    public static void OpenOutputFolder(string? configuredFolder)
    {
        var folder = ResolveOutputFolder(configuredFolder);
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

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

    /// <summary>
    /// Treat a stem as taken if either .wav or .mp3 already exists (avoid clobbering after conversion).
    /// </summary>
    private static bool StemTaken(string directory, string stemWithoutExtension)
    {
        var wav = Path.Combine(directory, stemWithoutExtension + ".wav");
        var mp3 = Path.Combine(directory, stemWithoutExtension + ".mp3");
        return File.Exists(wav) || File.Exists(mp3);
    }
}
