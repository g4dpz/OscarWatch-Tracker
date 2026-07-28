namespace OscarWatch.Core.Radio;

/// <summary>
/// Maps SmartSDR panadapter stream IDs to VHF/UHF SCUs for dual-pan satellite layouts.
/// Pan centres reflect each display's band, not the slice currently attached to that pan.
/// </summary>
public static class FlexPanBandResolver
{
    public static void ResolveTargetFrequencies(
        long downlinkHz,
        long uplinkHz,
        bool satelliteMode,
        out long vhfHz,
        out long uhfHz)
    {
        vhfHz = 0;
        uhfHz = 0;

        if (downlinkHz > 0)
            AssignBandTarget(downlinkHz, ref vhfHz, ref uhfHz);

        if (satelliteMode && uplinkHz > 0)
            AssignBandTarget(uplinkHz, ref vhfHz, ref uhfHz);
    }

    public static bool TryResolveBandPans(
        IEnumerable<FlexPanState> pans,
        out string? vhfPanStreamId,
        out string? uhfPanStreamId)
    {
        vhfPanStreamId = null;
        uhfPanStreamId = null;

        var candidates = pans
            .Where(p => !string.IsNullOrWhiteSpace(p.StreamId) && p.CenterHz > 0)
            .ToList();

        var vhf = candidates
            .Where(p => RigSatModeHelper.IsVhfCenterKHz(p.CenterHz / 1000.0))
            .OrderBy(p => p.StreamId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var uhf = candidates
            .Where(p => RigSatModeHelper.IsUhfCenterKHz(p.CenterHz / 1000.0))
            .OrderBy(p => p.StreamId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (vhf is not null)
            vhfPanStreamId = vhf.StreamId;
        if (uhf is not null)
            uhfPanStreamId = uhf.StreamId;

        return vhfPanStreamId is not null || uhfPanStreamId is not null;
    }

    private static void AssignBandTarget(long hz, ref long vhfHz, ref long uhfHz)
    {
        if (RigSatModeHelper.IsVhfCenterKHz(hz / 1000.0))
            vhfHz = hz;
        else if (RigSatModeHelper.IsUhfCenterKHz(hz / 1000.0))
            uhfHz = hz;
    }
}

public sealed record FlexPanState(string StreamId, long CenterHz, bool AutoCenter = false);
