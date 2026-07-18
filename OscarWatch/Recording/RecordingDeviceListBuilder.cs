using OscarWatch.Core.Services;

namespace OscarWatch.Recording;

internal readonly record struct RecordingDeviceCandidate(
    int Index,
    string Name,
    double DefaultLowInputLatency);

internal readonly record struct DeviceWithFormattedName(
    int Index,
    string RawName,
    string FormattedName,
    double DefaultLowInputLatency);

internal static class RecordingDeviceListBuilder
{
    internal static IReadOnlyList<AudioInputDevice> Build(IReadOnlyList<RecordingDeviceCandidate> candidates)
    {
        if (candidates.Count == 0)
            return [];

        // First pass: deduplicate by raw name, keep lowest latency
        var deduplicatedByRawName = candidates
            .GroupBy(candidate => candidate.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(candidate => candidate.DefaultLowInputLatency)
                .ThenBy(candidate => candidate.Index)
                .First())
            .ToList();

        // Format names and second pass: deduplicate by formatted name
        // (in case different raw names format to the same display name)
        var withFormattedNames = deduplicatedByRawName
            .Select(c =>
            {
                var rawName = c.Name.Trim();
                return new DeviceWithFormattedName(
                    c.Index,
                    rawName,
                    RecordingDeviceNameFormatter.Format(rawName),
                    c.DefaultLowInputLatency);
            })
            .ToList();

        var deduplicatedByFormattedName = withFormattedNames
            .GroupBy(c => c.FormattedName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(c => c.DefaultLowInputLatency)
                .ThenBy(c => c.Index)
                .First())
            .OrderBy(c => c.FormattedName, StringComparer.OrdinalIgnoreCase)
            .Select(c => new AudioInputDevice(c.RawName, c.FormattedName))
            .ToList();

        return deduplicatedByFormattedName;
    }
}
