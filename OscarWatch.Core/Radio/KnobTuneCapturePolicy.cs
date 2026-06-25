namespace OscarWatch.Core.Radio;

/// <summary>
/// Minimum Main-dial movement (Hz) before linear passband-trim capture treats a change as operator tuning.
/// Ignores small CAT/display jitter; FM modes use a wider window (passband trim is not used on FM).
/// </summary>
public static class KnobTuneCapturePolicy
{
    public const int LinearThresholdHz = 30;
    public const int FmThresholdHz = 250;

    public static int Resolve(string? downlinkMode)
    {
        if (string.IsNullOrWhiteSpace(downlinkMode))
            return LinearThresholdHz;

        var mode = TransponderCatModes.Normalize(downlinkMode);
        return mode switch
        {
            "FM" or "FMN" or "DATA-FM" => FmThresholdHz,
            _ => LinearThresholdHz
        };
    }
}
