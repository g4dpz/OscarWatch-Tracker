using OscarWatch.Core.Models;

namespace OscarWatch.Core.Ft4;

/// <summary>
/// When CAT Doppler is held between FT4 slot steps. FT4 is not a radio mode, so the
/// trigger is the FT4 window being open or the FT4 transponder being selected.
/// Anything else releases the hold so linear transponders track continuously.
/// </summary>
public static class Ft4SlotGate
{
    public const string TransponderType = "FT4";

    public static bool IsFt4Transponder(SatelliteTransponderMode? mode) =>
        mode is not null && mode.Type.Trim().Equals(TransponderType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The dial only moves on the steps the running FT4 session sends each slot,
    /// so the hold needs a running session or the dial would freeze.
    /// </summary>
    public static bool ShouldHold(bool sessionRunning, bool windowOpen, SatelliteTransponderMode? mode) =>
        sessionRunning && (windowOpen || IsFt4Transponder(mode));
}
