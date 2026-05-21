using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IRigController
{
    RigConnectionStatus GetStatus();
    void Update(RigSettings settings, RigTrackingContext? context);
    void Disconnect();
}
