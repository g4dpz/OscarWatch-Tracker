// Feature: ts2000-hardware-validation, Property 1: Frequency Round-Trip
// ∀ hz ∈ [100_000, 470_000_000]: TryParseFrequencyHz(BuildSetFrequencyCommand('A', hz)) == hz

using FsCheck.Xunit;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

/// <summary>
/// Property-based test validating that all valid frequencies round-trip through
/// BuildSetFrequencyCommand and TryParseFrequencyHz without loss.
///
/// Validates: Requirements 4.5, 12.5
/// </summary>
public class Ts2000FrequencyRoundTripPropertyTests
{
    /// <summary>
    /// Property 1: Frequency Round-Trip
    /// ∀ hz ∈ [100_000, 470_000_000]: TryParseFrequencyHz(BuildSetFrequencyCommand('A', hz)) == hz
    ///
    /// **Validates: Requirements 4.5, 12.5**
    /// </summary>
    [Property(MaxTest = 1000)]
    public bool Frequency_roundtrips_through_BuildSetFrequencyCommand_and_TryParse(long seed)
    {
        // Constrain to valid TS-2000 frequency range: 100 kHz to 470 MHz
        var hz = Math.Abs(seed) % (470_000_000 - 100_000) + 100_000;

        var command = KenwoodCatCodec.BuildSetFrequencyCommand('A', hz);
        var parsed = KenwoodCatCodec.TryParseFrequencyHz(command, out var result);

        return parsed && result == hz;
    }
}
