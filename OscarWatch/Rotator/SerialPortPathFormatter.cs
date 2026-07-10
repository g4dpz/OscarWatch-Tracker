namespace OscarWatch.Rotator;

public static class SerialPortPathFormatter
{
    public static string FormatDisplay(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (path.StartsWith("/dev/serial/by-id/", StringComparison.Ordinal))
            return "by-id: " + path["/dev/serial/by-id/".Length..];

        if (path.StartsWith("/dev/serial/by-path/", StringComparison.Ordinal))
            return "by-path: " + path["/dev/serial/by-path/".Length..];

        if (path.StartsWith("/dev/", StringComparison.Ordinal))
        {
            var name = Path.GetFileName(path);
            if (!name.StartsWith("tty", StringComparison.Ordinal))
                return name;
        }

        return path;
    }
}
