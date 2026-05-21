using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class IcomCivCodecTests
{
    public static IEnumerable<object[]> FrequencyEncodeRows()
    {
        foreach (var row in GoldenFixtureLoader.Load().FrequencyEncode)
            yield return new object[] { row.Hz, row.PayloadHex };
    }

    [Theory]
    [MemberData(nameof(FrequencyEncodeRows))]
    public void EncodeSetFrequencyHz_matches_golden(long hz, string expectedHex)
    {
        var body = IcomCivCodec.EncodeSetFrequencyHz(hz);
        Assert.Equal(expectedHex, Convert.ToHexString(body).ToLowerInvariant());
    }

    [Fact]
    public void ParseCivAddressHex_parses_hex_values()
    {
        Assert.Equal(0x60, IcomCivCodec.ParseCivAddressHex("60"));
        Assert.Equal(0x60, IcomCivCodec.ParseCivAddressHex(null));
        Assert.Equal(0x7C, IcomCivCodec.ParseCivAddressHex("7C"));
        Assert.Equal(0xA2, IcomCivCodec.ParseCivAddressHex("A2"));
    }

    [Fact]
    public void BuildCommandFrame_wraps_body()
    {
        var frame = IcomCivCodec.BuildCommandFrame(0x60, [0x03]);
        Assert.Equal(0xFE, frame[0]);
        Assert.Equal(0x60, frame[2]);
        Assert.Equal(0x03, frame[4]);
        Assert.Equal(0xFD, frame[^1]);
    }
}
