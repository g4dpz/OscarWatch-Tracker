using OscarWatch.Core.Tle;
using Zeptomoby.OrbitTools;
using SatelliteOrbit = Zeptomoby.OrbitTools.Orbit;

namespace OscarWatch.Tests;

public sealed class GpJsonPropagationTests
{
    [Fact]
    public void Json_derived_orbit_propagates_without_error()
    {
        const string json = """
            [{"AMSAT_NAME":"AO-07","OBJECT_NAME":"OSCAR 7","OBJECT_ID":"1974-089B","INCLINATION":101.9901,"ECCENTRICITY":0.00126647,"RA_OF_ASC_NODE":201.9731,"ARG_OF_PERICENTER":92.559,"MEAN_ANOMALY":74.3678,"MEAN_MOTION":12.53698425,"EPOCH":"2026-07-07T12:21:17.710848","NORAD_CAT_ID":7530,"REV_AT_EPOCH":36306,"BSTAR":4.948808e-06,"EPHEMERIS_TYPE":0,"CLASSIFICATION_TYPE":"U","ELEMENT_SET_NO":999,"MEAN_MOTION_DDOT":0.0,"MEAN_MOTION_DOT":-4.6e-07}]
            """;

        const string seedCatalog = """
            AO-07
            1 07530U 74089B   26141.31992461 -.00000054  00000-0 -48931-4 0  9992
            2 07530 101.9910 154.2858 0012269 180.6108 191.1977 12.53697584357151
            """;

        var seedEntry = TleParser.ParseCatalog(seedCatalog).Single();
        var jsonEntry = GpJsonCatalogParser.ParseCatalog(json).Single();

        var seedOrbit = new SatelliteOrbit(new Tle(seedEntry.Name, seedEntry.Line1, seedEntry.Line2));
        var jsonOrbit = new SatelliteOrbit(new Tle(jsonEntry.Name, jsonEntry.Line1, jsonEntry.Line2));

        var utc = new DateTime(2026, 7, 7, 14, 0, 0, DateTimeKind.Utc);
        var seedGeo = new GeoTime(seedOrbit.PositionEci(utc));
        var jsonGeo = new GeoTime(jsonOrbit.PositionEci(utc));

        Assert.InRange(jsonGeo.LatitudeDeg, seedGeo.LatitudeDeg - 5, seedGeo.LatitudeDeg + 5);
        Assert.InRange(jsonGeo.LongitudeDeg, seedGeo.LongitudeDeg - 5, seedGeo.LongitudeDeg + 5);
    }
}
