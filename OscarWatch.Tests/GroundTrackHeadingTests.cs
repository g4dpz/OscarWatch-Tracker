using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class GroundTrackHeadingTests
{
    [Fact]
    public void EstimateHeadingDeg_returns_east_for_west_to_east_track()
    {
        GeoCoordinate[] track =
        [
            new(0, 0),
            new(0, 10),
            new(0, 20)
        ];
        var subpoint = new GeoCoordinate(0, 10);

        var heading = GroundTrackHeading.EstimateHeadingDeg(subpoint, track);

        Assert.NotNull(heading);
        Assert.InRange(heading!.Value, 85, 95);
    }

    [Fact]
    public void EstimateHeadingDeg_returns_null_for_short_track()
    {
        GeoCoordinate[] track = [new(0, 0)];

        Assert.Null(GroundTrackHeading.EstimateHeadingDeg(new GeoCoordinate(0, 0), track));
    }
}
