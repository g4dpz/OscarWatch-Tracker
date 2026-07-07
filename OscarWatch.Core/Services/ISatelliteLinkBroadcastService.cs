using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ISatelliteLinkBroadcastService
{
    event Action? StateChanged;

    bool IsListening { get; }

    int ClientCount { get; }

    string? LastError { get; }

    void ApplySettings(SatelliteLinkSettings settings);

    void Publish(SatelliteTrackState? track, RigTrackingContext? context, bool force = false);

    Task<bool> TestBindAsync(SatelliteLinkSettings settings, CancellationToken cancellationToken = default);

    Task StopAsync();
}
