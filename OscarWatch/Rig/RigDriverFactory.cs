using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public static class RigDriverFactory
{
    public static IRigDriver Create(RigSettings settings) => settings.Type switch
    {
        RigType.IcomIc910 => new IcomIc910Driver(settings.Port, settings.BaudRate, settings.CivAddress),
        RigType.IcomIc9700 => new IcomIc9700Driver(settings.Port, settings.BaudRate, settings.CivAddress),
        RigType.Dummy => new DummyRigDriver(),
        _ => new DummyRigDriver()
    };
}
