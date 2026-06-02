using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public sealed class YaesuFt991aDriver : YaesuFt991Driver
{
    public YaesuFt991aDriver(string port, int baudRate, RigRegion region = RigRegion.EU, int catDelayMs = 50)
        : base(RigType.YaesuFt991a, port, baudRate, region, catDelayMs)
    {
    }

    internal YaesuFt991aDriver(IYaesuNewCatTransport transport, RigRegion region = RigRegion.EU, int catDelayMs = 50)
        : base(RigType.YaesuFt991a, transport, region, catDelayMs)
    {
    }
}
