namespace OscarWatch.Core.Radio;

public static class RigSatModeHelper
{
    /// <summary>True when Main/Sub satellite layout (bands &gt; 10 MHz apart).</summary>
    public static bool UseMainSubLayout(double downlinkKHz, double uplinkKHz) =>
        Math.Abs(downlinkKHz - uplinkKHz) > 10_000;
}
