using System.Runtime.InteropServices;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class RisingPassAnnouncer
{
    private readonly Dictionary<string, SatelliteAnnouncementState> _states = new(StringComparer.Ordinal);

    public void Process(
        IReadOnlyList<SatelliteTrackState> states,
        VoiceAnnouncementSettings settings,
        Action<string> speak)
    {
        if (!settings.Enabled)
            return;

        var trigger = settings.AnnounceElevationDeg;
        var active = new HashSet<string>(StringComparer.Ordinal);

        foreach (var state in states)
        {
            active.Add(state.NoradId);
            var elevation = state.LookAngles?.ElevationDeg ?? -90.0;
            ref var tracking = ref CollectionsMarshal.GetValueRefOrAddDefault(_states, state.NoradId, out _);
            tracking ??= new SatelliteAnnouncementState();

            if (!tracking.HasSample)
            {
                tracking.HasSample = true;
                tracking.PreviousElevationDeg = elevation;
                continue;
            }

            if (elevation < trigger)
            {
                tracking.AnnouncedForPass = false;
            }
            else if (!tracking.AnnouncedForPass
                     && tracking.PreviousElevationDeg < trigger
                     && elevation >= trigger
                     && elevation > tracking.PreviousElevationDeg)
            {
                speak(SatelliteNamePhonetics.FormatRisingAnnouncement(state.Name));
                tracking.AnnouncedForPass = true;
            }

            tracking.PreviousElevationDeg = elevation;
        }

        var stale = _states.Keys.Where(id => !active.Contains(id)).ToList();
        foreach (var id in stale)
            _states.Remove(id);
    }

    private sealed class SatelliteAnnouncementState
    {
        public bool HasSample;
        public double PreviousElevationDeg = -90.0;
        public bool AnnouncedForPass;
    }
}
