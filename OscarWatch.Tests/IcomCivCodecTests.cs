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

    [Theory]
    [InlineData(145_950_000)]
    [InlineData(435_659_900)]
    [InlineData(432_146_000)]
    public void DecodeFrequencyFromResponse_parses_bcd_digits_as_decimal_hz(long hz)
    {
        var body = IcomCivCodec.EncodeSetFrequencyHz(hz);
        var response = new byte[] { 0xFE, 0xFE, 0x60, 0x00, 0x00, body[1], body[2], body[3], body[4], body[5], 0xFB, 0xFD };
        var decoded = IcomCivCodec.DecodeFrequencyFromResponse(response);
        Assert.Equal(hz, decoded);
    }

    [Fact]
    public void DecodeFrequencyFromResponse_does_not_treat_digit_string_as_hex()
    {
        var decoded = IcomCivCodec.DecodeFrequencyFromResponse(
            BuildReadResponseFromHz(435_659_900));
        Assert.Equal(435_659_900, decoded);
        Assert.NotEqual(18_075_719_936, decoded);
    }

    [Theory]
    [InlineData(67.0, false, "1b000670")]
    [InlineData(67.0, true, "1b010670")]
    [InlineData(74.4, true, "1b010744")]
    [InlineData(141.3, false, "1b001413")]
    public void EncodeToneHz_matches_icom_civ_layout(double hz, bool squelchTone, string expectedHex)
    {
        var body = IcomCivCodec.EncodeToneHz(hz, squelchTone);
        Assert.Equal(expectedHex, Convert.ToHexString(body).ToLowerInvariant());
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

    [Theory]
    [InlineData("FM", "0605")]
    [InlineData("FMN", "0605")]
    [InlineData("fm", "0605")]
    [InlineData("USB", "0601")]
    [InlineData("LSB", "0600")]
    [InlineData("DATA-USB", "0601")]
    [InlineData("DATA-LSB", "0600")]
    [InlineData("CW", "0603")]
    public void EncodeSetModeCommand_maps_modes(string mode, string expectedHex)
    {
        var body = IcomCivCodec.EncodeSetModeCommand(mode);
        Assert.NotNull(body);
        Assert.Equal(expectedHex, Convert.ToHexString(body).ToLowerInvariant());
    }

    [Theory]
    [InlineData("FM")]
    [InlineData("FMN")]
    public void EncodeSetModeCommand_fm_never_uses_rtty_byte(string mode)
    {
        var body = IcomCivCodec.EncodeSetModeCommand(mode);
        Assert.NotNull(body);
        Assert.DoesNotContain((byte)0x04, body);
        Assert.Equal(0x05, body[1]);
    }

    private static byte[] BuildReadResponseFromHz(long hz)
    {
        var body = IcomCivCodec.EncodeSetFrequencyHz(hz);
        return [0xFE, 0xFE, 0x60, 0x00, 0x00, body[1], body[2], body[3], body[4], body[5], 0xFB, 0xFD];
    }
}
