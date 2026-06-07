using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>Yaesu FTX-1 Field / FTX-1optima — same newcat subset as FT-991 (dual-radio VFO-A only).</summary>
public sealed class YaesuFtx1Driver : YaesuFt991Driver
{
    public YaesuFtx1Driver(string port, int baudRate, RigRegion region = RigRegion.EU, int catDelayMs = 50)
        : base(RigType.YaesuFtx1, port, baudRate, region, catDelayMs)
    {
    }

    internal YaesuFtx1Driver(IYaesuNewCatTransport transport, RigRegion region = RigRegion.EU, int catDelayMs = 50)
        : base(RigType.YaesuFtx1, transport, region, catDelayMs)
    {
    }
}
