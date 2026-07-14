using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IGpsService : IDisposable
{
    void Update(GpsSettings settings);

    void Disconnect();

    /// <summary>Disconnect and block until the GPS worker has closed the serial port or gpsd socket.</summary>
    void DisconnectAndWait();

    GpsConnectionStatus GetStatus();

    /// <summary>GPS UTC when time sync is enabled and a recent fix exists; otherwise null.</summary>
    DateTime? GetTrackingUtc();
}
