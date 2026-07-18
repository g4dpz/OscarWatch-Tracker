using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

public static class RotatorDriverFactory
{
    public static IRotatorDriver Create(RotatorSettings settings) =>
        settings.Type switch
        {
            RotatorType.EasyComm => new EasyCommRotator(CreateTransport(settings, 1000, 1000, "\n")),
            RotatorType.Spid => new SpidRotator(CreateTransport(settings, 2000, 2000, "\n")),
            RotatorType.Saebrt => new SaebrtRotator(
                CreateTransport(settings, 200, 200, "\n", dtrEnable: false, rtsEnable: false)),
            RotatorType.UrcTcp => new UrcTcpRotator(
                settings.NetworkHost,
                settings.NetworkPort > 0 ? settings.NetworkPort : RotatorSettings.DefaultNetworkPort),
            _ => new Gs232Rotator(CreateTransport(settings, 1000, 1000, "\r"))
        };

    private static IRotatorSerialTransport CreateTransport(
        RotatorSettings settings,
        int readTimeoutMs,
        int writeTimeoutMs,
        string newLine,
        bool dtrEnable = false,
        bool rtsEnable = false) =>
        RotatorSerialTransportFactory.Create(
            settings,
            readTimeoutMs,
            writeTimeoutMs,
            newLine,
            dtrEnable,
            rtsEnable);
}
