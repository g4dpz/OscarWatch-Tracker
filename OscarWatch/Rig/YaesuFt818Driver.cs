using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>Yaesu FT-818 — same CAT protocol as FT-817.</summary>
public sealed class YaesuFt818Driver : YaesuFt817Driver
{
    public YaesuFt818Driver(string port, int baudRate, RigRegion region = RigRegion.EU, int catDelayMs = 50)
        : base(RigType.YaesuFt818, port, baudRate, region, catDelayMs)
    {
    }

    internal YaesuFt818Driver(IYaesuCatTransport transport, RigRegion region = RigRegion.EU, int catDelayMs = 50)
        : base(RigType.YaesuFt818, transport, region, catDelayMs)
    {
    }
}
