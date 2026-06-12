using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IRotatorController
{
    /// <summary>Enqueue latest target for the rotator worker (~1 Hz from UI).</summary>
    void Update(RotatorSettings settings, SatelliteTrackState? target);
    void Park(RotatorSettings settings);
    /// <summary>Manual az/el move while standby is active (browsing mode).</summary>
    void MoveTo(double azimuthDeg, double elevationDeg, RotatorSettings settings);
    void Stop(RotatorSettings settings);
    void SetStandby(bool active, RotatorSettings settings);
    void Disconnect();
    /// <summary>Supply the active pass for keyhole avoidance planning. Call when the pass changes or becomes known.</summary>
    void SetActivePass(PassInfo? pass);
    RotatorPositionStatus GetPositionStatus();
}
