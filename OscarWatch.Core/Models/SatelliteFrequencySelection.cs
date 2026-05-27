namespace OscarWatch.Core.Models;

public sealed class SatelliteFrequencySelection
{
    public string ModeType { get; set; } = "";
    /// <summary>Index into the satellite's modes array; used when multiple modes exist.</summary>
    public int ModeIndex { get; set; }
    public bool RememberOffsets { get; set; } = true;
    /// <summary>Legacy single-mode offsets; migrated into <see cref="ModeOffsets"/> on load.</summary>
    public double TransmitOffsetKHz { get; set; }
    public double ReceiveOffsetKHz { get; set; }
    public Dictionary<string, ModeOffsetSettings> ModeOffsets { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>"access" or "arm" when the mode offers both CTCSS tones.</summary>
    public string CtcssToneRole { get; set; } = "access";

    /// <summary>Per-mode CW-on-uplink override for linear voice transponders.</summary>
    public Dictionary<string, bool> CwUplinkByMode { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool GetCwUplinkForMode(string modeType)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return false;

        return CwUplinkByMode.TryGetValue(modeType.Trim(), out var cw) && cw;
    }

    public void SetCwUplinkForMode(string modeType, bool cwUplink)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return;

        var key = modeType.Trim();
        if (cwUplink)
            CwUplinkByMode[key] = true;
        else
            CwUplinkByMode.Remove(key);
    }

    public (double TransmitOffsetKHz, double ReceiveOffsetKHz) GetOffsetsForMode(string modeType)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return (0, 0);

        if (ModeOffsets.TryGetValue(modeType.Trim(), out var mode))
            return (mode.TransmitOffsetKHz, mode.ReceiveOffsetKHz);

        if (modeType.Equals(ModeType, StringComparison.OrdinalIgnoreCase)
            && (TransmitOffsetKHz != 0 || ReceiveOffsetKHz != 0))
            return (TransmitOffsetKHz, ReceiveOffsetKHz);

        return (0, 0);
    }

    public void SetOffsetsForMode(string modeType, double transmitOffsetKHz, double receiveOffsetKHz)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return;

        var key = modeType.Trim();
        if (!ModeOffsets.TryGetValue(key, out var mode))
        {
            mode = new ModeOffsetSettings();
            ModeOffsets[key] = mode;
        }

        mode.TransmitOffsetKHz = transmitOffsetKHz;
        mode.ReceiveOffsetKHz = receiveOffsetKHz;
        TransmitOffsetKHz = transmitOffsetKHz;
        ReceiveOffsetKHz = receiveOffsetKHz;
        ModeType = key;
    }
}
