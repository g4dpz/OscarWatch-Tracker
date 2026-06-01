using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public static class RigDriverFactory
{
    public static IRigDriver Create(RigSettings settings)
    {
        if (settings.DualRadioEnabled)
            throw new InvalidOperationException("Use Create(RigEndpointSettings) for dual-radio endpoints.");

        return CreateSingle(settings.Type, settings.Port, settings.BaudRate, settings.Region, settings.CatDelayMs, settings.CivAddress);
    }

    public static IRigDriver Create(RigEndpointSettings endpoint) =>
        CreateSingle(endpoint.Type, endpoint.Port, endpoint.BaudRate, endpoint.Region, endpoint.CatDelayMs, civAddress: "60");

    private static IRigDriver CreateSingle(
        RigType type,
        string port,
        int baudRate,
        RigRegion region,
        int catDelayMs,
        string civAddress)
    {
        return type switch
        {
            RigType.IcomIc910 => new IcomIc910Driver(port, baudRate, civAddress, catDelayMs),
            RigType.IcomIc9100 => new IcomIc9100Driver(port, baudRate, civAddress, catDelayMs),
            RigType.IcomIc9700 => new IcomIc9700Driver(port, baudRate, civAddress, catDelayMs),
            RigType.YaesuFt847 => new YaesuFt847Driver(port, baudRate, catDelayMs),
            RigType.YaesuFt817 => new YaesuFt817Driver(RigType.YaesuFt817, port, baudRate, region, catDelayMs),
            RigType.YaesuFt818 => new YaesuFt818Driver(port, baudRate, region, catDelayMs),
            RigType.KenwoodTs2000 => new KenwoodTs2000Driver(port, baudRate, catDelayMs),
            RigType.Dummy => new DummyRigDriver(),
            _ => new DummyRigDriver()
        };
    }
}
