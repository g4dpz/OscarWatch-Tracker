namespace OscarWatch.Rotator;

public static class SerialPortCatalog
{
    private const string ByIdDirectory = "/dev/serial/by-id";
    private const string ByPathDirectory = "/dev/serial/by-path";

    public static IReadOnlyList<string> BuildDisplayList(
        IEnumerable<string> systemPorts,
        IEnumerable<string> extraPaths,
        Func<string, string?>? resolveDevicePath = null)
    {
        var resolve = resolveDevicePath ?? TryResolveDevicePath;
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in systemPorts)
        {
            if (!string.IsNullOrWhiteSpace(port))
                candidates.Add(port.Trim());
        }

        foreach (var path in extraPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
                candidates.Add(path.Trim());
        }

        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in candidates)
        {
            var resolved = resolve(path) ?? path;
            if (!groups.TryGetValue(resolved, out var group))
            {
                group = [];
                groups[resolved] = group;
            }

            group.Add(path);
        }

        return groups.Values
            .Select(SelectPreferredPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IEnumerable<string> EnumerateLinuxStablePaths()
    {
        foreach (var path in EnumerateDirectoryEntries(ByIdDirectory))
            yield return path;

        foreach (var path in EnumerateDirectoryEntries(ByPathDirectory))
            yield return path;

        foreach (var path in EnumerateLinuxUdevAliases())
            yield return path;
    }

    public static string? TryResolveDevicePath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (!File.Exists(path))
                return null;

            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    internal static int GetPathPriority(string path)
    {
        if (IsLinuxUdevAlias(path))
            return 0;

        if (path.StartsWith("/dev/serial/by-id/", StringComparison.Ordinal))
            return 1;

        if (path.StartsWith("/dev/serial/by-path/", StringComparison.Ordinal))
            return 2;

        return 3;
    }

    private static string SelectPreferredPath(IReadOnlyList<string> paths)
    {
        return paths
            .OrderBy(GetPathPriority)
            .ThenBy(path => path.Length)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static IEnumerable<string> EnumerateDirectoryEntries(string directory)
    {
        if (!Directory.Exists(directory))
            yield break;

        string[] entries;
        try
        {
            entries = Directory.GetFiles(directory);
        }
        catch
        {
            yield break;
        }

        foreach (var entry in entries.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            yield return entry;
    }

    private static IEnumerable<string> EnumerateLinuxUdevAliases()
    {
        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries("/dev");
        }
        catch
        {
            yield break;
        }

        foreach (var entry in entries.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(entry);
            if (string.IsNullOrEmpty(name))
                continue;

            if (!IsCandidateUdevAliasName(name))
                continue;

            if (Directory.Exists(entry))
                continue;

            FileInfo info;
            try
            {
                info = new FileInfo(entry);
            }
            catch
            {
                continue;
            }

            if (info.LinkTarget is null)
                continue;

            var resolved = TryResolveDevicePath(entry);
            if (resolved is null || !IsSerialDevicePath(resolved))
                continue;

            yield return entry;
        }
    }

    private static bool IsCandidateUdevAliasName(string name)
    {
        if (name.StartsWith("tty", StringComparison.Ordinal)
            || name.StartsWith("serial", StringComparison.Ordinal)
            || name.StartsWith("bus", StringComparison.Ordinal)
            || name.StartsWith("char", StringComparison.Ordinal)
            || name.StartsWith("disk", StringComparison.Ordinal)
            || name.StartsWith("input", StringComparison.Ordinal)
            || name.StartsWith("block", StringComparison.Ordinal)
            || name.StartsWith("mapper", StringComparison.Ordinal)
            || name.StartsWith("fd", StringComparison.Ordinal)
            || name.StartsWith("loop", StringComparison.Ordinal)
            || name.StartsWith("ram", StringComparison.Ordinal)
            || name.StartsWith("nvram", StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool IsLinuxUdevAlias(string path)
    {
        if (!path.StartsWith("/dev/", StringComparison.Ordinal)
            || path.StartsWith("/dev/serial/", StringComparison.Ordinal))
            return false;

        var name = Path.GetFileName(path);
        return !name.StartsWith("tty", StringComparison.Ordinal);
    }

    private static bool IsSerialDevicePath(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("ttyUSB", StringComparison.Ordinal)
            || name.StartsWith("ttyACM", StringComparison.Ordinal)
            || name.StartsWith("rfcomm", StringComparison.Ordinal);
    }
}
