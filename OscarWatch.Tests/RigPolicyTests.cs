using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class RigSatModeHelperTests
{
    public static IEnumerable<object[]> RigSatModeRows()
    {
        foreach (var row in GoldenFixtureLoader.Load().RigSatMode)
            yield return new object[] { row.DownlinkKHz, row.UplinkKHz, row.UseMainSub };
    }

    [Theory]
    [MemberData(nameof(RigSatModeRows))]
    public void UseMainSubLayout_matches_golden(double down, double up, bool expected) =>
        Assert.Equal(expected, RigSatModeHelper.UseMainSubLayout(down, up));
}

public class SetupVfosPolicyTests
{
    [Theory]
    [InlineData("FMN", 200, 50, 200, false)]
    [InlineData("USB", 200, 50, 50, true)]
    [InlineData("DATA-USB", 200, 50, 0, false)]
    public void Evaluate_returns_expected_threshold(
        string mode, int fm, int linear, int expectedThreshold, bool interactive)
    {
        var result = SetupVfosPolicy.Evaluate(mode, fm, linear);
        Assert.Equal(expectedThreshold, result.ThresholdHz);
        Assert.Equal(interactive, result.Interactive);
    }
}

public class RigSettingsSerializationTests
{
    [Fact]
    public void RigSettings_defaults()
    {
        var s = new RigSettings();
        Assert.Equal(RigType.None, s.Type);
        Assert.Equal(RigRegion.EU, s.Region);
        Assert.Equal("7C", RigSettings.DefaultCivAddressFor(RigType.IcomIc910));
        Assert.Equal("A2", RigSettings.DefaultCivAddressFor(RigType.IcomIc9700));
    }
}
