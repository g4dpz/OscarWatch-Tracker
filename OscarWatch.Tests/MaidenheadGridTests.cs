using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

public sealed class MaidenheadGridTests
{
    // Reference grids from Cloudlog HamGridSquare.js (QST / N5JTY test vectors).
    public static TheoryData<string, double, double, int> CloudlogVectors =>
        new()
        {
            { "JN58td", 48.14666, 11.60833, 6 },
            { "GF15vc", -34.91, -56.21166, 6 },
            { "FM18lw", 38.92, -77.065, 6 },
            { "RE78ir", -41.28333, 174.745, 6 },
            { "FN31pr", 41.714775, -72.727260, 6 },
            { "CM87wj", 37.413708, -122.1073236, 6 },
            { "EM75kb", 35.0542, -85.1142, 6 },
        };

    [Theory]
    [MemberData(nameof(CloudlogVectors))]
    public void FromLatLon_matches_cloudlog_reference(string expectedGrid, double lat, double lon, int length)
    {
        var grid = MaidenheadGrid.FromLatLon(lat, lon, length);
        Assert.Equal(expectedGrid, grid);
    }

    [Theory]
    [MemberData(nameof(CloudlogVectors))]
    public void ToLatLonCenter_round_trips_cloudlog_reference(string grid, double lat, double lon, int _)
    {
        var (centerLat, centerLon) = MaidenheadGrid.ToLatLonCenter(grid);
        var roundTrip = MaidenheadGrid.FromLatLon(centerLat, centerLon);

        Assert.Equal(grid, roundTrip);
        // Input lat/lon are arbitrary points inside the cell, not necessarily the centre.
        Assert.InRange(centerLat, lat - 0.5, lat + 0.5);
        Assert.InRange(centerLon, lon - 1.0, lon + 1.0);
    }

    [Fact]
    public void ToLatLonCenter_io71lx_matches_expected_center()
    {
        var (lat, lon) = MaidenheadGrid.ToLatLonCenter("io71lx");

        Assert.InRange(lat, 51.978, 51.980);
        Assert.InRange(lon, -5.042, -5.041);
    }

    [Fact]
    public void ToLatLonCenter_four_char_uses_square_center()
    {
        var (lat, lon) = MaidenheadGrid.ToLatLonCenter("IO71");

        Assert.InRange(lat, 51.49, 51.51);
        Assert.InRange(lon, -5.01, -4.99);
    }

    [Fact]
    public void ToLatLonCenter_six_char_does_not_add_square_center_offset()
    {
        var (fourLat, fourLon) = MaidenheadGrid.ToLatLonCenter("IO71");
        var (sixLat, sixLon) = MaidenheadGrid.ToLatLonCenter("IO71aa");

        Assert.True(sixLat < fourLat - 0.4);
        Assert.True(sixLon < fourLon - 0.9);
    }
}
