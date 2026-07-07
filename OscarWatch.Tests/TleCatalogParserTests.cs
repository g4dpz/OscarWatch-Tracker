using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

public sealed class TleCatalogParserTests
{
  private const string TextCatalog = """
        AO-07
        1 07530U 74089B   26141.31992461 -.00000054  00000-0 -48931-4 0  9992
        2 07530 101.9910 154.2858 0012269 180.6108 191.1977 12.53697584357151
        """;

    private const string JsonCatalog = """
        [{"AMSAT_NAME":"AO-07","OBJECT_NAME":"OSCAR 7","OBJECT_ID":"1974-089B","INCLINATION":101.9901,"ECCENTRICITY":0.00126647,"RA_OF_ASC_NODE":201.9731,"ARG_OF_PERICENTER":92.559,"MEAN_ANOMALY":74.3678,"MEAN_MOTION":12.53698425,"EPOCH":"2026-07-07T12:21:17.710848","NORAD_CAT_ID":7530,"REV_AT_EPOCH":36306,"BSTAR":4.948808e-06,"EPHEMERIS_TYPE":0,"CLASSIFICATION_TYPE":"U","ELEMENT_SET_NO":999,"MEAN_MOTION_DDOT":0.0,"MEAN_MOTION_DOT":-4.6e-07}]
        """;

    [Fact]
    public void Auto_detects_text_catalog()
    {
        var entries = TleCatalogParser.ParseCatalog(TextCatalog);
        Assert.Single(entries);
        Assert.Equal("AO-07", entries[0].Name);
    }

    [Fact]
    public void Auto_detects_json_catalog()
    {
        var entries = TleCatalogParser.ParseCatalog(JsonCatalog);
        Assert.Single(entries);
        Assert.Equal("AO-07", entries[0].Name);
    }
}
