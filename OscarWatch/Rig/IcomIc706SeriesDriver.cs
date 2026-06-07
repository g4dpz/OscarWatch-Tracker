using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>
/// IC-706 / IC-706MKII / IC-706MKIIG CI-V driver for dual-radio endpoints (one VFO per physical radio).
/// No dedicated satellite mode — dual layout uses VFO A only.
/// </summary>
public sealed class IcomIc706SeriesDriver : IcomCivDriverBase
{
    public IcomIc706SeriesDriver(
        RigType rigType,
        string port,
        int baudRate,
        string civAddressHex,
        int catDelayMs = 50)
        : base(rigType, port, baudRate, civAddressHex, catDelayMs)
    {
    }

    internal IcomIc706SeriesDriver(RigType rigType, IIcomCivTransport transport)
        : base(rigType, transport)
    {
    }

    public override bool SupportsTracking => true;

    public override void SetSatelliteMode(bool on)
    {
    }

    protected override RigVfo MapOperationalVfo(RigVfo vfo) =>
        vfo is RigVfo.Main or RigVfo.Sub ? RigVfo.VfoA : vfo;
}
