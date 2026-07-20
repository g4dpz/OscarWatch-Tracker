using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class FlexDiscoveryCodecTests
{
    private const string SampleAscii =
        "discovery_protocol_version=2.0.0.0 model=FLEX-6600 serial=3615-5017-6600-4899 " +
        "version=3.5.8 nickname=Shack callsign=G3WGV ip=192.168.0.42 port=4992 status=Available";

    [Fact]
    public void TryParseAscii_ExtractsFields()
    {
        Assert.True(FlexDiscoveryCodec.TryParseAscii(SampleAscii, out var radio));
        Assert.Equal("192.168.0.42", radio.IpAddress);
        Assert.Equal(4992, radio.Port);
        Assert.Equal("3615-5017-6600-4899", radio.Serial);
        Assert.Equal("FLEX-6600", radio.Model);
        Assert.Equal("Shack", radio.Nickname);
        Assert.Equal("G3WGV", radio.Callsign);
        Assert.Equal("Available", radio.Status);
    }

    [Fact]
    public void TryParse_PlainAsciiBytes()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(SampleAscii);
        Assert.True(FlexDiscoveryCodec.TryParse(bytes, out var radio));
        Assert.Equal("FLEX-6600", radio.Model);
    }

    [Fact]
    public void TryParse_VitaWrappedAscii_FindsSerial()
    {
        // Minimal VITA-like header (28 bytes) + ASCII discovery payload
        var ascii = System.Text.Encoding.ASCII.GetBytes(SampleAscii);
        var packet = new byte[28 + ascii.Length];
        // Class ID trailing FFFF at offsets 14-15
        packet[14] = 0xFF;
        packet[15] = 0xFF;
        ascii.CopyTo(packet.AsSpan(28));

        Assert.True(FlexDiscoveryCodec.TryParse(packet, out var radio));
        Assert.Equal("192.168.0.42", radio.IpAddress);
        Assert.Equal("FLEX-6600", radio.Model);
    }

    [Fact]
    public void TryParseAscii_InfersModelFromSerialWhenMissing()
    {
        const string text =
            "serial=3615-5017-6700-0001 ip=10.0.0.5 port=4992";
        Assert.True(FlexDiscoveryCodec.TryParseAscii(text, out var radio));
        Assert.Equal("FLEX-6700", radio.Model);
    }

    [Fact]
    public void TryParseAscii_DefaultsPortWhenMissing()
    {
        Assert.True(FlexDiscoveryCodec.TryParseAscii("ip=10.0.0.1 serial=x", out var radio));
        Assert.Equal(FlexDiscoveryCodec.DefaultDiscoveryPort, radio.Port);
    }

    [Fact]
    public void TryParseAscii_RequiresIp()
    {
        Assert.False(FlexDiscoveryCodec.TryParseAscii("serial=abc model=FLEX-6600", out _));
    }

    [Theory]
    [InlineData("FLEX-6600", true)]
    [InlineData("FLEX-6600M", true)]
    [InlineData("FLEX-6700", true)]
    [InlineData("FLEX-6400", false)]
    [InlineData("FLEX-6400M", false)]
    [InlineData("", false)]
    public void LooksDuplexCapable(string model, bool expected) =>
        Assert.Equal(expected, FlexDiscoveryCodec.LooksDuplexCapable(model));

    [Fact]
    public void FormatDisplayName_IncludesNicknameModelAndEndpoint()
    {
        var radio = new FlexDiscoveredRadio(
            "192.168.1.10", 4992, "s", "FLEX-6600", "Shack", "", "", "", "");
        var display = FlexDiscoveryCodec.FormatDisplayName(radio);
        Assert.Contains("Shack", display);
        Assert.Contains("FLEX-6600", display);
        Assert.Contains("192.168.1.10:4992", display);
    }
}
