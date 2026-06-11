using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Verifies that after the task 7.1 refactoring, the RigController computes Doppler exactly once
/// per tick (not twice), halving propagator GetLookAngles calls compared to the old dual-computation path.
/// Validates: Requirements 5.2, 5.3
/// </summary>
public class RigControllerSingleDopplerTests
{
    private static readonly GroundStation Site = new()
    {
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50
    };

    /// <summary>
    /// Verifies that a single tracking loop tick calls GetLookAngles the number of times consistent
    /// with a single ComputeDoppler invocation (slope sample + lead calls), not doubled.
    ///
    /// With DopplerCatLeadEnabled and a steep slope, one ComputeDoppler call triggers:
    ///   1 slope sample + 1 rxLead + 1 txLead = 3 GetLookAngles calls.
    /// If Doppler were computed twice (the old path), we'd see 6 calls.
    /// After the refactoring, we expect exactly 3 calls per tick.
    /// </summary>
    [Fact]
    public void Single_tick_calls_GetLookAngles_once_per_doppler_computation()
    {
        var propagator = new CountingPropagator(steepSlope: true);
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig, propagator: propagator);

        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerThresholdLinearHz = 50,
            DopplerCatLeadEnabled = true,
            CatDelayMs = 100
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var state = new SatelliteTrackState
        {
            Name = "FO-29",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0, 400),
            LookAngles = new LookAngles(180, 45, 800, -3.5)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, -3.5, 0),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        // Initial Update calls WriteDopplerFrequencies which computes Doppler once (pass init + first tick).
        controller.Update(settings, ctx);
        controller.DrainCommandQueueForTests();

        // Record baseline call count after init.
        var callsAfterInit = propagator.GetLookAnglesCallCount;

        // Run one tracking loop tick — should compute Doppler exactly once.
        controller.RunTrackingLoopOnce();

        var callsForOneTick = propagator.GetLookAnglesCallCount - callsAfterInit;

        // With steep slope and CatDelayMs = 100: one ComputeDoppler invocation triggers
        // ResolveRangeRates → 1 slope sample + 1 rxLead + 1 txLead = 3 calls.
        // If the old dual path were still active, we'd see 6.
        Assert.True(callsForOneTick <= 3,
            $"Expected at most 3 GetLookAngles calls per tick (single Doppler computation), but got {callsForOneTick}. " +
            "This suggests Doppler is still being computed more than once per tick.");
    }

    /// <summary>
    /// Verifies that display frequencies (DisplayReceiveHz, DisplayTransmitHz) after a tracking loop
    /// tick match the expected Doppler-corrected values from a single ComputeDoppler call.
    /// </summary>
    [Fact]
    public void Display_frequencies_match_single_doppler_computation()
    {
        var propagator = new CountingPropagator(steepSlope: true);
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig, propagator: propagator);

        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerThresholdLinearHz = 50,
            DopplerCatLeadEnabled = true,
            CatDelayMs = 100
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var state = new SatelliteTrackState
        {
            Name = "FO-29",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0, 400),
            LookAngles = new LookAngles(180, 45, 800, -3.5)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, -3.5, 0),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        controller.DrainCommandQueueForTests();

        // Run one tracking tick to ensure Doppler is applied.
        controller.RunTrackingLoopOnce();

        var status = controller.GetStatus();

        // Compute expected Doppler using the same lead range rates the propagator provides.
        // The CountingPropagator returns rxLeadRate=-2.0 and txLeadRate=-2.0, with blend=1.0 on steep slope.
        // So the blended rates are: Lerp(-3.5, -2.0, 1.0) = -2.0 for both rx and tx.
        var expectedCorrected = DopplerFrequencyCalculator.Compute(mode, -2.0, 0, transmitRangeRateKmPerSec: -2.0);
        var expectedRxHz = (long)Math.Round(expectedCorrected.RadioReceiveKHz * 1000.0);
        var expectedTxHz = (long)Math.Round(expectedCorrected.RadioTransmitKHz * 1000.0);

        Assert.NotNull(status.LastReceiveHz);
        Assert.NotNull(status.LastTransmitHz);
        Assert.Equal(expectedRxHz, status.LastReceiveHz!.Value);
        Assert.Equal(expectedTxHz, status.LastTransmitHz!.Value);
    }

    /// <summary>
    /// Verifies that with DopplerCatLead disabled, a single tick does not call GetLookAngles at all
    /// (confirming ComputeDoppler goes through the disabled path without extra calls).
    /// </summary>
    [Fact]
    public void Disabled_cat_lead_produces_zero_propagator_calls_per_tick()
    {
        var propagator = new CountingPropagator(steepSlope: true);
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig, propagator: propagator);

        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerThresholdLinearHz = 50,
            DopplerCatLeadEnabled = false,
            CatDelayMs = 100
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var state = new SatelliteTrackState
        {
            Name = "FO-29",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0, 400),
            LookAngles = new LookAngles(180, 45, 800, -3.5)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, -3.5, 0),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        controller.DrainCommandQueueForTests();

        var callsAfterInit = propagator.GetLookAnglesCallCount;
        controller.RunTrackingLoopOnce();

        var callsForOneTick = propagator.GetLookAnglesCallCount - callsAfterInit;

        Assert.Equal(0, callsForOneTick);
    }

    /// <summary>
    /// A propagator stub that counts GetLookAngles calls and returns predictable range rates.
    /// When steepSlope=true, any call with a utc ahead of the base time returns a slope that triggers
    /// the lead path. Lead calls return a distinct rate for verification.
    /// </summary>
    private sealed class CountingPropagator(bool steepSlope) : IOrbitPropagator
    {
        public int GetLookAnglesCallCount { get; private set; }

        private const double SnapshotRate = -3.5;
        private const double LeadRate = -2.0;

        // When steep: all propagator calls return -2.0, producing:
        //   slope = |(-2.0) - (-3.5)| / 1.0 = 1.5 km/s² → well above SteepRangeRateSlopeKmPerSec2 (0.018)
        //   blend = 1.0
        //   lead rates = -2.0
        //   blended result = Lerp(-3.5, -2.0, 1.0) = -2.0 for both rx and tx.
        // When not steep: returns -3.5 (same as snapshot), slope = 0 → blend = 0 → returns fallback.

        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => ["99999"];

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
        {
            GetLookAnglesCallCount++;
            var rate = steepSlope ? LeadRate : SnapshotRate;
            return new LookAngles(180, 45, 800, rate);
        }
    }
}
