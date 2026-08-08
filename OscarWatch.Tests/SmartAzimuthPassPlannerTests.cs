using OscarWatch.Core.Models;
using OscarWatch.Core.Rotator;

namespace OscarWatch.Tests;

public sealed class SmartAzimuthPassPlannerTests
{
    private static PassProfile ProfileFromAzimuths(
        DateTime aosUtc,
        params double[] azimuthsDeg)
    {
        var points = new PassProfilePoint[azimuthsDeg.Length];
        for (var i = 0; i < azimuthsDeg.Length; i++)
        {
            points[i] = new PassProfilePoint(
                aosUtc.AddSeconds(i),
                azimuthsDeg[i],
                20);
        }

        var pass = new PassInfo
        {
            SatelliteName = "TEST",
            NoradId = "44909",
            AosUtc = aosUtc,
            LosUtc = aosUtc.AddSeconds(Math.Max(azimuthsDeg.Length - 1, 0)),
            MaxElevationDeg = 40,
            MaxElevationUtc = aosUtc.AddSeconds(azimuthsDeg.Length / 2),
            AosAzimuthDeg = azimuthsDeg[0],
            LosAzimuthDeg = azimuthsDeg[^1]
        };

        return new PassProfile(pass, points);
    }

    [Fact]
    public void Analyse_southeast_pass_stays_primary()
    {
        var aos = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var profile = ProfileFromAzimuths(aos, 3, 15, 45, 90, 135);
        var plan = SmartAzimuthPassPlanner.Analyse(profile, maxAzimuthDeg: 450, startCommandAzDeg: 135);

        Assert.NotNull(plan);
        Assert.All(plan.Samples, s => Assert.Equal(SmartAzimuthBand.Primary, s.Band));
    }

    [Fact]
    public void Analyse_westbound_north_wrap_uses_extended()
    {
        var aos = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        // Approach from west, cross north into NE, then jump west after TCA.
        var profile = ProfileFromAzimuths(aos, 350, 355, 5, 15, 330, 320);
        var plan = SmartAzimuthPassPlanner.Analyse(profile, maxAzimuthDeg: 450, startCommandAzDeg: 350);

        Assert.NotNull(plan);
        Assert.Contains(plan.Samples, s => s.Band == SmartAzimuthBand.Extended);
        // After the west-side points, primary is expected for az > 270 without +360 available... 
        // 330 and 320 cannot use extended (330+360 > 450), so those samples are Primary.
        Assert.Equal(SmartAzimuthBand.Primary, plan.Samples[^1].Band);
        Assert.Equal(SmartAzimuthBand.Primary, plan.Samples[^2].Band);
    }

    [Fact]
    public void Analyse_returns_null_for_360_range()
    {
        var aos = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var profile = ProfileFromAzimuths(aos, 350, 10);
        Assert.Null(SmartAzimuthPassPlanner.Analyse(profile, maxAzimuthDeg: 360, startCommandAzDeg: 350));
    }

    [Fact]
    public void LookupBand_returns_sample_for_time_in_window()
    {
        var aos = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var profile = ProfileFromAzimuths(aos, 3, 15, 45);
        var plan = SmartAzimuthPassPlanner.Analyse(profile, 450, 135)!;

        Assert.Equal(SmartAzimuthBand.Primary, SmartAzimuthPassPlanner.LookupBand(plan, aos.AddSeconds(1)));
        Assert.Null(SmartAzimuthPassPlanner.LookupBand(plan, aos.AddHours(-1)));
        Assert.Null(SmartAzimuthPassPlanner.LookupBand(null, aos));
    }

    [Fact]
    public void ResolveCommandAz_preferred_extended_forces_overlap()
    {
        Assert.Equal(
            375,
            RotatorAzimuthPlanner.ResolveCommandAz(
                135,
                15,
                450,
                preferredBand: SmartAzimuthBand.Extended));
    }

    [Fact]
    public void ResolveCommandAz_preferred_primary_ignores_myopic_overlap()
    {
        // Without preferred band, 370→20 would unwrap; with Primary forced, still primary.
        Assert.Equal(
            20,
            RotatorAzimuthPlanner.ResolveCommandAz(
                370,
                20,
                450,
                preferredBand: SmartAzimuthBand.Primary));
    }
}
