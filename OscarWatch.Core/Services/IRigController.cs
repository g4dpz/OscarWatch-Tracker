using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IRigController
{
    RigConnectionStatus GetStatus();

    /// <summary>Enqueue latest pass/settings for the dedicated rig thread (~1–4 Hz from UI).</summary>
    /// <param name="reinitializePass">When true and the pass key is unchanged, re-run SAT mode / frequency setup (e.g. user re-selected a satellite). When false, offset-only updates force an immediate doppler write.</param>
    /// <param name="catPausedOverride">When non-null, overrides <see cref="RigSettings.CatUpdatesPaused"/> without cloning the settings object.</param>
    void PublishContext(RigSettings settings, RigTrackingContext? context, bool reinitializePass = false, bool? catPausedOverride = null);

    /// <summary>Synchronous publish + doppler tick on the rig thread (unit tests).</summary>
    void Update(RigSettings settings, RigTrackingContext? context);

    /// <summary>User changed the CTCSS selector — always program uplink Sub/VFO B (even if CAT paused).</summary>
    void ApplySelectedCtcss(RigSettings settings, RigTrackingContext? context);

    void Disconnect();
}
