namespace OscarWatch.Recording;

/// <summary>
/// Resolves a saved recording device to a PortAudio index using durable name identity
/// (not a volatile enumeration index). Used after USB re-enumeration / reboot.
/// </summary>
internal static class RecordingDeviceResolver
{
    internal readonly record struct InputDeviceSnapshot(
        int Index,
        string RawName,
        double DefaultLowInputLatency,
        int MaxInputChannels);

    /// <summary>
    /// Returns the PortAudio device index to open, or -1 if no match.
    /// </summary>
    internal static int ResolveIndex(
        string? deviceId,
        string? deviceDisplayName,
        IReadOnlyList<InputDeviceSnapshot> inputs)
    {
        if (inputs.Count == 0)
            return -1;

        var id = deviceId?.Trim() ?? "";
        var displayName = deviceDisplayName?.Trim() ?? "";
        var legacyIndex = TryParseLegacyIndex(id);

        if (legacyIndex is null && id.Length > 0)
        {
            var byRaw = FindBestByRawName(id, inputs);
            if (byRaw >= 0)
                return byRaw;
        }

        if (displayName.Length > 0)
        {
            var byDisplay = FindBestByFormattedDisplayName(displayName, inputs);
            if (byDisplay >= 0)
                return byDisplay;
        }

        // Legacy numeric id: only honour if the device at that index still matches the saved display name
        if (legacyIndex is { } index)
        {
            var atIndex = inputs.FirstOrDefault(d => d.Index == index && d.MaxInputChannels > 0);
            if (atIndex.MaxInputChannels > 0)
            {
                if (displayName.Length == 0)
                    return -1;

                var formattedAtIndex = RecordingDeviceNameFormatter.Format(atIndex.RawName);
                var formattedStored = RecordingDeviceNameFormatter.Format(displayName);
                if (formattedAtIndex.Equals(formattedStored, StringComparison.OrdinalIgnoreCase))
                    return index;

                // Index moved: try display name was already attempted above; also try raw match on id is N/A
            }
        }

        // Last resort for legacy: if we only have a numeric id and no display name, do not open by index alone
        return -1;
    }

    internal static bool IsLegacyNumericDeviceId(string? deviceId) =>
        TryParseLegacyIndex(deviceId?.Trim() ?? "") is not null;

    private static int? TryParseLegacyIndex(string id)
    {
        if (id.Length == 0)
            return null;
        // Pure decimal index only (legacy). Raw PortAudio names are never solely digits in practice,
        // but we treat any all-digit id as legacy so old settings migrate safely.
        for (var i = 0; i < id.Length; i++)
        {
            if (!char.IsAsciiDigit(id[i]))
                return null;
        }

        if (!int.TryParse(id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index))
            return null;
        return index;
    }

    private static int FindBestByRawName(string rawName, IReadOnlyList<InputDeviceSnapshot> inputs)
    {
        var matches = inputs
            .Where(d => d.MaxInputChannels > 0
                        && d.RawName.Trim().Equals(rawName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.DefaultLowInputLatency)
            .ThenBy(d => d.Index)
            .ToList();
        return matches.Count > 0 ? matches[0].Index : -1;
    }

    private static int FindBestByFormattedDisplayName(string displayName, IReadOnlyList<InputDeviceSnapshot> inputs)
    {
        var formattedStored = RecordingDeviceNameFormatter.Format(displayName);
        if (formattedStored.Length == 0)
            return -1;

        var matches = inputs
            .Where(d =>
            {
                if (d.MaxInputChannels <= 0)
                    return false;
                var formatted = RecordingDeviceNameFormatter.Format(d.RawName);
                return formatted.Equals(formattedStored, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(d => d.DefaultLowInputLatency)
            .ThenBy(d => d.Index)
            .ToList();
        return matches.Count > 0 ? matches[0].Index : -1;
    }
}
