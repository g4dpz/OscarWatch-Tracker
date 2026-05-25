using OscarWatch.Core.Services;

namespace OscarWatch.Recording;

internal readonly record struct RecordingDeviceCandidate(
    int Index,
    string Name,
    double DefaultLowInputLatency);

internal static class RecordingDeviceListBuilder
{
    internal static IReadOnlyList<AudioInputDevice> Build(IReadOnlyList<RecordingDeviceCandidate> candidates)
    {
        if (candidates.Count == 0)
            return [];

        return candidates
            .GroupBy(candidate => candidate.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(candidate => candidate.DefaultLowInputLatency)
                .ThenBy(candidate => candidate.Index)
                .First())
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new AudioInputDevice(candidate.Index.ToString(), candidate.Name))
            .ToList();
    }
}
