using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public sealed class IcomIc9700Driver : IcomCivDriverBase
{
    public IcomIc9700Driver(string port, int baudRate, string civAddressHex)
        : base(RigType.IcomIc9700, port, baudRate, civAddressHex)
    {
    }

    public override bool SupportsTracking => false;

    public override void SetSatelliteMode(bool on) =>
        WriteWithRetry(on ? [0x16, 0x5A, 0x01] : [0x16, 0x5A, 0x00]);
}
