using System.IO.Ports;

namespace OscarWatch.Rotator;

public static class SerialPortDiscovery
{
    private static IReadOnlyList<string>? _cachedPorts;
    private static readonly object CacheGate = new();

    public static IReadOnlyList<string> GetAvailablePorts(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            lock (CacheGate)
            {
                if (_cachedPorts is not null)
                    return _cachedPorts;
            }
        }

        var ports = DiscoverPorts();

        lock (CacheGate)
            _cachedPorts = ports;

        return ports;
    }

    private static IReadOnlyList<string> DiscoverPorts()
    {
        try
        {
            var systemPorts = SerialPort.GetPortNames()
                .Where(port => !string.IsNullOrWhiteSpace(port))
                .Select(port => port.Trim())
                .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!OperatingSystem.IsLinux())
                return systemPorts;

            var extraPaths = SerialPortCatalog.EnumerateLinuxStablePaths().ToArray();
            return SerialPortCatalog.BuildDisplayList(systemPorts, extraPaths);
        }
        catch
        {
            return [];
        }
    }
}
