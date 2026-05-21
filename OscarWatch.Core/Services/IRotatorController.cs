using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IRotatorController
{
    void Update(RotatorSettings settings, SatelliteTrackState? target);
    void Park(RotatorSettings settings);
    void Disconnect();
    RotatorPositionStatus GetPositionStatus();
}
