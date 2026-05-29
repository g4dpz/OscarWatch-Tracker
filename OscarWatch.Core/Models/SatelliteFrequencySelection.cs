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

    /// <summary>Per-mode receive offset when CW operating style is selected on linear voice transponders.</summary>
    public Dictionary<string, double> CwReceiveOffsetKHzByMode { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-mode Doppler strategy (omitted entries default to <see cref="DopplerStrategy.Full"/>).</summary>
    public Dictionary<string, DopplerStrategy> DopplerStrategyByMode { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public DopplerStrategy GetDopplerStrategyForMode(string modeType)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return DopplerStrategy.Full;

        return DopplerStrategyByMode.TryGetValue(modeType.Trim(), out var strategy)
            ? strategy
            : DopplerStrategy.Full;
    }

    public void SetDopplerStrategyForMode(string modeType, DopplerStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return;

        var key = modeType.Trim();
        if (strategy == DopplerStrategy.Full)
            DopplerStrategyByMode.Remove(key);
        else
            DopplerStrategyByMode[key] = strategy;
    }

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

    public double GetReceiveOffsetForMode(string modeType, bool cwOperatingStyle)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return 0;

        var key = modeType.Trim();
        if (cwOperatingStyle
            && CwReceiveOffsetKHzByMode.TryGetValue(key, out var cwRx))
            return cwRx;

        return GetOffsetsForMode(key).ReceiveOffsetKHz;
    }

    public void SetReceiveOffsetForMode(string modeType, double receiveOffsetKHz, bool cwOperatingStyle)
    {
        if (string.IsNullOrWhiteSpace(modeType))
            return;

        var key = modeType.Trim();
        if (cwOperatingStyle)
        {
            if (Math.Abs(receiveOffsetKHz) < 0.0001)
                CwReceiveOffsetKHzByMode.Remove(key);
            else
                CwReceiveOffsetKHzByMode[key] = receiveOffsetKHz;
            return;
        }

        SetOffsetsForMode(key, 0, receiveOffsetKHz);
    }
}
