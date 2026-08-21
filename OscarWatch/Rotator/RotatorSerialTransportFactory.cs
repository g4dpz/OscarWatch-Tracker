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
        bool rtsEnable = false) =>
        Create(
            settings,
            settings.Port,
            readTimeoutMs,
            writeTimeoutMs,
            newLine,
            dtrEnable,
            rtsEnable);

    public static IRotatorSerialTransport Create(
        RotatorSettings settings,
        string portName,
        int readTimeoutMs,
        int writeTimeoutMs,
        string newLine,
        bool dtrEnable = false,
        bool rtsEnable = false)
    {
        if (settings.TransportKind == RotatorTransportKind.Tcp
            && settings.Type != RotatorType.GreenHeronRt21)
        {
            return new TcpRotatorTransport(
                settings.NetworkHost,
                settings.NetworkPort > 0 ? settings.NetworkPort : RotatorSettings.DefaultNetworkPort,
                readTimeoutMs,
                writeTimeoutMs,
                newLine);
        }

        return new SerialRotatorTransport(
            portName,
            settings.BaudRate,
            readTimeoutMs,
            writeTimeoutMs,
            newLine,
            dtrEnable,
            rtsEnable);
    }
}
