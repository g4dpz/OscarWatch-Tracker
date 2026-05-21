using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

public static class RotatorDriverFactory
{
    public static IRotatorDriver Create(RotatorSettings settings) =>
        settings.Type switch
        {
            RotatorType.EasyComm => new EasyCommRotator(settings.Port, settings.BaudRate),
            _ => new Gs232Rotator(settings.Port, settings.BaudRate)
        };
}
