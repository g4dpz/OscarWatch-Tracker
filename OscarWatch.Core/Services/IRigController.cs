using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IRigController
{
    RigConnectionStatus GetStatus();

    /// <summary>Enqueue latest pass/settings for the dedicated rig thread (~1–4 Hz from UI).</summary>
    void PublishContext(RigSettings settings, RigTrackingContext? context);

    /// <summary>Synchronous publish + doppler tick on the rig thread (unit tests).</summary>
    void Update(RigSettings settings, RigTrackingContext? context);

    /// <summary>User changed the CTCSS selector — always program uplink Sub/VFO B (even if CAT paused).</summary>
    void ApplySelectedCtcss(RigSettings settings, RigTrackingContext? context);

    void Disconnect();
}
