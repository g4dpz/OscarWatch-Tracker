using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

public static class RotatorDriverFactory
{
    public static IRotatorDriver Create(RotatorSettings settings) =>
        settings.Type switch
        {
            RotatorType.EasyComm => new EasyCommRotator(settings.Port, settings.BaudRate),
            RotatorType.Spid => new SpidRotator(settings.Port, settings.BaudRate),
            RotatorType.Saebrt => new SaebrtRotator(settings.Port, settings.BaudRate),
            RotatorType.UrcTcp => new UrcTcpRotator(
                settings.NetworkHost,
                settings.NetworkPort > 0 ? settings.NetworkPort : RotatorSettings.DefaultNetworkPort),
            _ => new Gs232Rotator(settings.Port, settings.BaudRate)
        };
}
