using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>
/// IC-9700 CI-V (QTrig <c>setSatelliteMode9700</c>: 0x16 0x5A). Shares freq/VFO/tone path with IC-910.
/// </summary>
public sealed class IcomIc9700Driver : IcomCivDriverBase
{
    public IcomIc9700Driver(string port, int baudRate, string civAddressHex)
        : base(RigType.IcomIc9700, port, baudRate, civAddressHex)
    {
    }

    public override bool SupportsTracking => true;

    public override void SetSatelliteMode(bool on) =>
        WriteWithRetry(on ? [0x16, 0x5A, 0x01] : [0x16, 0x5A, 0x00]);
}
