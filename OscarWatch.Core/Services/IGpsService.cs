using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IGpsService : IDisposable
{
    void Update(GpsSettings settings);

    void Disconnect();

    GpsConnectionStatus GetStatus();

    /// <summary>GPS UTC when time sync is enabled and a recent fix exists; otherwise null.</summary>
    DateTime? GetTrackingUtc();
}
