using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>
/// IC-9100 CI-V satellite mode (0x16 0x5A). Same Main/Sub/VFO/tone path as IC-9700 (Hamlib ic9700_set_vfo).
/// </summary>
public sealed class IcomIc9100Driver : IcomCivDriverBase
{
    public IcomIc9100Driver(string port, int baudRate, string civAddressHex, int catDelayMs = 50)
        : base(RigType.IcomIc9100, port, baudRate, civAddressHex, catDelayMs)
    {
    }

    public override bool SupportsTracking => true;

    public override void SetSatelliteMode(bool on) =>
        WriteWithRetry(on ? [0x16, 0x5A, 0x01] : [0x16, 0x5A, 0x00]);
}
