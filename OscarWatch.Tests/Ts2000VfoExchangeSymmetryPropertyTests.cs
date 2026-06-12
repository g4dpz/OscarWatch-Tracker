// Feature: ts2000-hardware-validation, Property 5: VFO Exchange Symmetry

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 8.1, 8.2**
///
/// Property 5: VFO Exchange Symmetry
/// ∀ valid (fa, fb): ExchangeVfos swaps cached Main/Sub; calling twice returns to original state.
/// </summary>
public class Ts2000VfoExchangeSymmetryPropertyTests
{
    /// <summary>
    /// Property 5: For any valid frequency pair (fa, fb) in satellite bands,
    /// calling ExchangeVfos once swaps Main and Sub cached frequencies,
    /// and calling ExchangeVfos a second time returns them to the original values.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ExchangeVfos_twice_returns_to_original_state(int faSeed, int fbSeed)
    {
        var faHz = GenerateValidFrequency(faSeed, isDownlink: true);
        var fbHz = GenerateValidFrequency(fbSeed, isDownlink: false);

        var transport = new RecordingKenwoodCatTransport
        {
            FaHz = faHz,
            FbHz = fbHz,
            SatelliteStatusOn = true
        };

        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);

        // Read original frequencies
        var originalMainHz = driver.ReadFrequencyHz(RigVfo.Main);
        var originalSubHz = driver.ReadFrequencyHz(RigVfo.Sub);

        if (originalMainHz is null or <= 0 || originalSubHz is null or <= 0)
            return false;

        // First exchange: should swap Main and Sub
        driver.ExchangeVfos();

        var afterFirstMainHz = driver.ReadFrequencyHz(RigVfo.Main);
        var afterFirstSubHz = driver.ReadFrequencyHz(RigVfo.Sub);

        if (afterFirstMainHz != originalSubHz || afterFirstSubHz != originalMainHz)
            return false;

        // Second exchange: should return to original state
        driver.ExchangeVfos();

        var afterSecondMainHz = driver.ReadFrequencyHz(RigVfo.Main);
        var afterSecondSubHz = driver.ReadFrequencyHz(RigVfo.Sub);

        return afterSecondMainHz == originalMainHz && afterSecondSubHz == originalSubHz;
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
