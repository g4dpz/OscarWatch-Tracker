using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IRotatorController
{
    /// <summary>Enqueue latest target for the rotator worker (~1 Hz from UI).</summary>
    void Update(RotatorSettings settings, SatelliteTrackState? target);
    void Park(RotatorSettings settings);
    void SetStandby(bool active, RotatorSettings settings);
    void Disconnect();
    RotatorPositionStatus GetPositionStatus();
}
