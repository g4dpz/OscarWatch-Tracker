using System.IO.Ports;

namespace OscarWatch.Rotator;

public static class SerialPortDiscovery
{
    public static IReadOnlyList<string> GetAvailablePorts()
    {
        try
        {
            var systemPorts = SerialPort.GetPortNames();
            var extraPaths = OperatingSystem.IsLinux()
                ? SerialPortCatalog.EnumerateLinuxStablePaths()
                : [];

            return SerialPortCatalog.BuildDisplayList(systemPorts, extraPaths);
        }
        catch
        {
            return [];
        }
    }
}
