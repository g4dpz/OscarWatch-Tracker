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
        if (!Directory.Exists("/dev"))
            yield break;

        var yielded = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries("/dev"))
        {
            if (yielded >= 64)
                yield break;

            var name = Path.GetFileName(entry);
            if (!IsLikelyCustomUdevAliasName(name))
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

            yielded++;
            yield return entry;
        }
    }

    internal static bool IsLikelyCustomUdevAliasName(string name)
    {
        if (name.Length is < 3 or > 24)
            return false;

        if (!IsCandidateUdevAliasName(name))
            return false;

        // Typical udev SYMLINK names (USB821H, ttyICR9K). Kernel ttyUSB0 / tty0 are rejected separately.
        if (!name.Any(char.IsLetter))
            return false;

        return name.All(static c => char.IsLetterOrDigit(c) || c is '_' or '-');
    }

    private static bool IsCandidateUdevAliasName(string name)
    {
        if (IsKernelTtyDeviceName(name)
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

    /// <summary>
    /// Kernel tty nodes (ttyUSB0, ttyS0, tty0) as opposed to ham udev aliases (ttyICR9K, ttyFT736).
    /// </summary>
    internal static bool IsKernelTtyDeviceName(string name)
    {
        if (!name.StartsWith("tty", StringComparison.Ordinal))
            return false;

        var rest = name[3..];
        if (rest.Length == 0 || rest.All(char.IsDigit))
            return true;

        if (name.Equals("ttyprintk", StringComparison.Ordinal))
            return true;

        foreach (var prefix in KernelTtyDriverPrefixes)
        {
            if (!rest.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var suffix = rest[prefix.Length..];
            if (suffix.Length > 0 && suffix.All(char.IsDigit))
                return true;
        }

        return rest[0] == 'p' && rest.Length > 1 && rest[1..].All(char.IsDigit);
    }

    private static readonly string[] KernelTtyDriverPrefixes =
    [
        "XRUSB", "SAC", "THS", "MFD", "MSM", "USB", "ACM", "AMA", "MAX",
        "GS", "HS", "AP", "PS", "S", "O"
    ];

    private static bool IsLinuxUdevAlias(string path)
    {
        if (!path.StartsWith("/dev/", StringComparison.Ordinal)
            || path.StartsWith("/dev/serial/", StringComparison.Ordinal))
            return false;

        return IsLikelyCustomUdevAliasName(Path.GetFileName(path));
    }

    private static bool IsSerialDevicePath(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("ttyUSB", StringComparison.Ordinal)
            || name.StartsWith("ttyACM", StringComparison.Ordinal)
            || name.StartsWith("rfcomm", StringComparison.Ordinal);
    }
}
