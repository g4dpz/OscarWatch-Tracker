using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IRigController
{
    RigConnectionStatus GetStatus();

    /// <summary>Refresh cached tracking context from the UI (~1 Hz). CAT doppler runs on a background loop.</summary>
    void PublishContext(RigSettings settings, RigTrackingContext? context);

    /// <summary>Legacy synchronous path (unit tests).</summary>
    void Update(RigSettings settings, RigTrackingContext? context);

    /// <summary>User changed the CTCSS selector — always program uplink Sub/VFO B (even if CAT paused).</summary>
    void ApplySelectedCtcss(RigSettings settings, RigTrackingContext? context);

    void Disconnect();
}
