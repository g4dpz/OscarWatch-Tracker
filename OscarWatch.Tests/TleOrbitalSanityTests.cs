using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

public sealed class TleOrbitalSanityTests
{
    [Theory]
    [InlineData(12.5, 101.99, 0.001)]
    [InlineData(15.5, 51.6, 0.0001)]
    [InlineData(1.0, 0.0, 0.0)]
    public void IsGpRecordPlausible_accepts_typical_amateur_orbits(
        double meanMotion,
        double inclination,
        double eccentricity)
    {
        var record = new GpElementRecord
        {
            AmsatName = "TEST",
            NoradCatId = 1,
            MeanMotion = meanMotion,
            Inclination = inclination,
            Eccentricity = eccentricity,
            RaOfAscNode = 0,
            ArgOfPericenter = 0,
            MeanAnomaly = 0,
            Epoch = "2026-07-07T12:00:00Z"
        };

        Assert.True(TleOrbitalSanity.IsGpRecordPlausible(record));
    }

    [Theory]
    [InlineData(150.0, 51.6, 0.001)]
    [InlineData(12.5, -1.0, 0.001)]
    [InlineData(12.5, 51.6, 1.5)]
    [InlineData(0.05, 51.6, 0.001)]
    public void IsGpRecordPlausible_rejects_implausible_orbits(
        double meanMotion,
        double inclination,
        double eccentricity)
    {
        var record = new GpElementRecord
        {
            AmsatName = "TEST",
            NoradCatId = 1,
            MeanMotion = meanMotion,
            Inclination = inclination,
            Eccentricity = eccentricity,
            RaOfAscNode = 0,
            ArgOfPericenter = 0,
            MeanAnomaly = 0,
            Epoch = "2026-07-07T12:00:00Z"
        };

        Assert.False(TleOrbitalSanity.IsGpRecordPlausible(record));
    }

    [Fact]
    public void ParseCatalogWithDiagnostics_skips_implausible_gp_json_records()
    {
        const string catalog = """
            [
                {
                    "AMSAT_NAME": "AO-07",
                    "OBJECT_NAME": "OSCAR 7",
                    "INCLINATION": 101.9901,
                    "ECCENTRICITY": 0.00126647,
                    "RA_OF_ASC_NODE": 201.9731,
                    "ARG_OF_PERICENTER": 92.559,
                    "MEAN_ANOMALY": 74.3678,
                    "MEAN_MOTION": 12.53698425,
                    "EPOCH": "2026-07-07T12:21:17.710848",
                    "NORAD_CAT_ID": 7530
                },
                {
                    "AMSAT_NAME": "CORRUPT",
                    "INCLINATION": 51.6,
                    "ECCENTRICITY": 0.001,
                    "RA_OF_ASC_NODE": 0,
                    "ARG_OF_PERICENTER": 0,
                    "MEAN_ANOMALY": 0,
                    "MEAN_MOTION": 150.0,
                    "EPOCH": "2026-07-07T12:21:17.710848",
                    "NORAD_CAT_ID": 99999
                }
            ]
            """;

        var result = GpJsonCatalogParser.ParseCatalogWithDiagnostics(catalog);

        Assert.Single(result.Entries);
        Assert.Equal("AO-07", result.Entries[0].Name);
        Assert.Equal(1, result.Diagnostics.SkippedOrbitalSanity);
    }
}
