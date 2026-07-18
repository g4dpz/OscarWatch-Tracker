using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

internal static class RotatorSerialTransportFactory
{
    public static IRotatorSerialTransport Create(
        RotatorSettings settings,
        int readTimeoutMs,
        int writeTimeoutMs,
        string newLine,
        bool dtrEnable = false,
        bool rtsEnable = false)
    {
        if (settings.TransportKind == RotatorTransportKind.Tcp)
        {
            return new TcpRotatorTransport(
                settings.NetworkHost,
                settings.NetworkPort > 0 ? settings.NetworkPort : RotatorSettings.DefaultNetworkPort,
                readTimeoutMs,
                writeTimeoutMs,
                newLine);
        }

        return new SerialRotatorTransport(
            settings.Port,
            settings.BaudRate,
            readTimeoutMs,
            writeTimeoutMs,
            newLine,
            dtrEnable,
            rtsEnable);
    }
}
