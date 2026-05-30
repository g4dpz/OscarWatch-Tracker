using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public sealed class IcomIc910Driver : IcomCivDriverBase
{
    public IcomIc910Driver(string port, int baudRate, string civAddressHex, int catDelayMs = 50)
        : base(RigType.IcomIc910, port, baudRate, civAddressHex, catDelayMs)
    {
    }

    internal IcomIc910Driver(IIcomCivTransport transport)
        : base(RigType.IcomIc910, transport)
    {
    }

    public override bool SupportsTracking => true;

    public override void SetSatelliteMode(bool on) =>
        WriteWithRetry(on ? [0x1A, 0x07, 0x01] : [0x1A, 0x07, 0x00]);
}
