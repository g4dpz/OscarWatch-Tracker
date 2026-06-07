// Feature: ts2000-hardware-validation, Property 2: Doppler Cluster Completeness

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 5.1, 5.2**
///
/// Property 2: Doppler Cluster Completeness
/// ∀ valid (downlink, uplink) where both > 0: ApplySatelliteDopplerStep produces
/// exactly 8 cluster commands + 7 link-hold polls = 15 total commands.
///
/// The cluster sequence is: FA, FB, SM10000, FA, SM-sub, FB, SM-sub, SM10000
/// followed by 7 FA; link-hold polls.
/// </summary>
public class Ts2000DopplerClusterPropertyTests
{
    /// <summary>
    /// Property 2: For any valid downlink/uplink frequency pair, ApplySatelliteDopplerStep
    /// produces exactly 15 total commands: 8 cluster + 7 link-hold polls.
    /// The first 8 commands follow the pattern (FA, FB, SM10000, FA, SM-sub, FB, SM-sub, SM10000)
    /// and the last 7 are all FA; (link-hold polls).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DopplerStep_produces_exactly_15_commands_with_correct_cluster_and_polls(
        int downlinkSeed, int uplinkSeed)
    {
        // Generate valid frequencies in ham radio satellite bands
        var downlinkHz = GenerateValidFrequency(downlinkSeed, isDownlink: true);
        var uplinkHz = GenerateValidFrequency(uplinkSeed, isDownlink: false);

        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        var result = driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        if (!result)
            return false;

        var cmds = transport.SentCommands;

        // Total must be exactly 15: 8 cluster + 7 polls
        if (cmds.Count != 15)
            return false;

        // Expected commands
        var expectedFa = $"FA{downlinkHz:D11};";
        var expectedFb = $"FB{uplinkHz:D11};";
        var expectedSmSub = downlinkHz >= 200_000_000 ? "SM00004;" : "SM00021;";

        // First 8 are the cluster: FA, FB, SM10000, FA, SM-sub, FB, SM-sub, SM10000
        if (cmds[0] != expectedFa) return false;
        if (cmds[1] != expectedFb) return false;
        if (cmds[2] != "SM10000;") return false;
        if (cmds[3] != expectedFa) return false;
        if (cmds[4] != expectedSmSub) return false;
        if (cmds[5] != expectedFb) return false;
        if (cmds[6] != expectedSmSub) return false;
        if (cmds[7] != "SM10000;") return false;

        // Last 7 are FA; link-hold polls
        for (var i = 8; i < 15; i++)
        {
            if (cmds[i] != "FA;")
                return false;
        }

        return true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid amateur satellite frequency from a seed integer.
    /// Constrains to realistic VHF/UHF ham bands used for satellite work.
    /// </summary>
    private static long GenerateValidFrequency(int seed, bool isDownlink)
    {
        // Use absolute value and modulo to constrain to valid ranges
        var absSeed = Math.Abs((long)seed);

        if (isDownlink)
        {
            // Downlink bands: 145.800-146.000 MHz (2m) or 435.000-438.000 MHz (70cm)
            if (absSeed % 2 == 0)
                return 145_800_000 + (absSeed % 200_000); // 145.800-146.000 MHz
            else
                return 435_000_000 + (absSeed % 3_000_000); // 435.000-438.000 MHz
        }
        else
        {
            // Uplink bands: 145.900-146.000 MHz (2m) or 435.000-438.000 MHz (70cm)
            if (absSeed % 2 == 0)
                return 145_900_000 + (absSeed % 100_000); // 145.900-146.000 MHz
            else
                return 435_000_000 + (absSeed % 3_000_000); // 435.000-438.000 MHz
        }
    }
}
