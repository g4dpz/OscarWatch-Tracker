using OscarWatch.Core.Tle;
using Zeptomoby.OrbitTools;
using SatelliteOrbit = Zeptomoby.OrbitTools.Orbit;

namespace OscarWatch.Tests;

public sealed class GpJsonCatalogParserTests
{
    private const string Ao07Sample = """
        [
            {
                "AMSAT_NAME": "AO-07",
                "OBJECT_NAME": "OSCAR 7",
                "OBJECT_ID": "1974-089B",
                "INCLINATION": 101.9901,
                "ECCENTRICITY": 0.00126647,
                "RA_OF_ASC_NODE": 201.9731,
                "ARG_OF_PERICENTER": 92.559,
                "MEAN_ANOMALY": 74.3678,
                "MEAN_MOTION": 12.53698425,
                "PERIOD": 114.86,
                "APOAPSIS": 1458.978,
                "PERIAPSIS": 1439.153,
                "COUNTRY_CODE": "US",
                "EPOCH": "2026-07-07T12:21:17.710848",
                "NORAD_CAT_ID": 7530,
                "REV_AT_EPOCH": 36306,
                "BSTAR": 4.948808e-06,
                "EPHEMERIS_TYPE": 0,
                "CLASSIFICATION_TYPE": "U",
                "ELEMENT_SET_NO": 999,
                "MEAN_MOTION_DDOT": 0.0,
                "MEAN_MOTION_DOT": -4.6e-07
            }
        ]
        """;

    [Fact]
    public void Parse_uses_amsat_name_not_object_name()
    {
        var entries = GpJsonCatalogParser.ParseCatalog(Ao07Sample);

        Assert.Single(entries);
        Assert.Equal("AO-07", entries[0].Name);
        Assert.Equal("7530", entries[0].NoradId);
        Assert.NotNull(entries[0].EpochUtc);
    }

    [Fact]
    public void Parse_produces_lines_accepted_by_orbit_tools()
    {
        var entry = GpJsonCatalogParser.ParseCatalog(Ao07Sample).Single();

        var tle = new Tle(entry.Name, entry.Line1, entry.Line2);
        Assert.StartsWith("1", tle.Line1);
        Assert.StartsWith("2", tle.Line2);
        Assert.Equal(69, tle.Line1.Length);
        Assert.Equal(69, tle.Line2.Length);

        _ = new SatelliteOrbit(tle);
    }

    [Fact]
    public void ResolveName_prefers_amsat_name()
    {
        var record = new GpElementRecord { AmsatName = "SO-50", ObjectName = "SAUDISAT 1C" };
        Assert.Equal("SO-50", GpJsonCatalogParser.ResolveName(record));
    }

    [Fact]
    public void ResolveName_falls_back_to_object_name()
    {
        var record = new GpElementRecord { ObjectName = "SAUDISAT 1C" };
        Assert.Equal("SAUDISAT 1C", GpJsonCatalogParser.ResolveName(record));
    }

    [Fact]
    public void Parse_skips_name_only_placeholder_entries()
    {
        // AMSAT daily-bulletin.json includes satellites announced by name before elements exist.
        const string catalog = """
            [
                {
                    "AMSAT_NAME": "AO-07",
                    "OBJECT_NAME": "OSCAR 7",
                    "OBJECT_ID": "1974-089B",
                    "INCLINATION": 101.9901,
                    "ECCENTRICITY": 0.00126647,
                    "RA_OF_ASC_NODE": 201.9731,
                    "ARG_OF_PERICENTER": 92.559,
                    "MEAN_ANOMALY": 74.3678,
                    "MEAN_MOTION": 12.53698425,
                    "EPOCH": "2026-07-07T12:21:17.710848",
                    "NORAD_CAT_ID": 7530,
                    "REV_AT_EPOCH": 36306,
                    "BSTAR": 4.948808e-06,
                    "EPHEMERIS_TYPE": 0,
                    "CLASSIFICATION_TYPE": "U",
                    "ELEMENT_SET_NO": 999,
                    "MEAN_MOTION_DDOT": 0.0,
                    "MEAN_MOTION_DOT": -4.6e-07
                },
                {
                    "AMSAT_NAME": "HYDRA-W",
                    "OBJECT_NAME": "",
                    "OBJECT_ID": null,
                    "INCLINATION": null,
                    "ECCENTRICITY": null,
                    "RA_OF_ASC_NODE": null,
                    "ARG_OF_PERICENTER": null,
                    "MEAN_ANOMALY": null,
                    "MEAN_MOTION": null,
                    "PERIOD": null,
                    "APOAPSIS": null,
                    "PERIAPSIS": null,
                    "COUNTRY_CODE": null,
                    "EPOCH": null,
                    "NORAD_CAT_ID": null,
                    "REV_AT_EPOCH": null,
                    "BSTAR": null,
                    "EPHEMERIS_TYPE": null,
                    "CLASSIFICATION_TYPE": null,
                    "ELEMENT_SET_NO": null,
                    "MEAN_MOTION_DDOT": null,
                    "MEAN_MOTION_DOT": null
                }
            ]
            """;

        var entries = GpJsonCatalogParser.ParseCatalog(catalog);

        Assert.Single(entries);
        Assert.Equal("AO-07", entries[0].Name);

        var diagnostics = GpJsonCatalogParser.ParseCatalogWithDiagnostics(catalog).Diagnostics;
        Assert.Equal(1, diagnostics.SkippedIncomplete);
    }
}
