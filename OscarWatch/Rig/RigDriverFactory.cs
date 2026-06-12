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

    public static IRigDriver Create(RigEndpointSettings endpoint) => endpoint.Type switch
    {
        RigType.YaesuFt817 => new YaesuFt817Driver(
            RigType.YaesuFt817, endpoint.Port, endpoint.BaudRate, endpoint.Region, endpoint.CatDelayMs),
        RigType.YaesuFt818 => new YaesuFt818Driver(
            endpoint.Port, endpoint.BaudRate, endpoint.Region, endpoint.CatDelayMs),
        RigType.YaesuFt991 => new YaesuFt991Driver(
            RigType.YaesuFt991, endpoint.Port, endpoint.BaudRate, endpoint.Region, endpoint.CatDelayMs),
        RigType.YaesuFt991a => new YaesuFt991aDriver(
            endpoint.Port, endpoint.BaudRate, endpoint.Region, endpoint.CatDelayMs),
        RigType.YaesuFtx1 => new YaesuFtx1Driver(
            endpoint.Port, endpoint.BaudRate, endpoint.Region, endpoint.CatDelayMs),
        RigType.IcomIc705 => new IcomIc705Driver(
            endpoint.Port, endpoint.BaudRate, ResolveEndpointCivAddress(endpoint), endpoint.CatDelayMs),
        RigType.IcomIc706 or RigType.IcomIc706Mkii or RigType.IcomIc706MkiiG =>
            CreateIc706SeriesDriver(endpoint),
        _ => CreateSingle(
            endpoint.Type,
            endpoint.Port,
            endpoint.BaudRate,
            endpoint.Region,
            endpoint.CatDelayMs,
            ResolveEndpointCivAddress(endpoint))
    };

    private static IRigDriver CreateIc706SeriesDriver(RigEndpointSettings endpoint) =>
        new IcomIc706SeriesDriver(
            endpoint.Type,
            endpoint.Port,
            endpoint.BaudRate,
            ResolveEndpointCivAddress(endpoint),
            endpoint.CatDelayMs);

    private static string ResolveEndpointCivAddress(RigEndpointSettings endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.CivAddress))
            return endpoint.CivAddress.Trim();

        return RigSettings.DefaultCivAddressFor(endpoint.Type);
    }

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
            RigType.IcomIc821h => new IcomIc821hDriver(port, baudRate, civAddress, catDelayMs),
            RigType.IcomIc705 =>
                throw new InvalidOperationException("IC-705 requires Settings → Radio → Dual radio."),
            RigType.IcomIc706 or RigType.IcomIc706Mkii or RigType.IcomIc706MkiiG =>
                throw new InvalidOperationException("IC-706 series radios require Settings → Radio → Dual radio."),
            RigType.YaesuFt847 => new YaesuFt847Driver(port, baudRate, catDelayMs),
            RigType.YaesuFt817 or RigType.YaesuFt818 =>
                throw new InvalidOperationException("FT-817/FT-818 require Settings → Radio → Dual radio."),
            RigType.YaesuFt991 or RigType.YaesuFt991a =>
                throw new InvalidOperationException("FT-991/FT-991A require Settings → Radio → Dual radio."),
            RigType.YaesuFtx1 =>
                throw new InvalidOperationException("FTX-1 series radios require Settings → Radio → Dual radio."),
            RigType.KenwoodTs2000 => new KenwoodTs2000Driver(port, baudRate, catDelayMs),
            RigType.Dummy => new DummyRigDriver(),
            _ => new DummyRigDriver()
        };
    }
}
