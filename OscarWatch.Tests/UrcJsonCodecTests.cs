using System.Text;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public class UrcJsonCodecTests
{
    [Fact]
    public void PollRequest_matches_protocol_literal()
    {
        Assert.Equal("{\"POLL\"}", UrcJsonCodec.PollRequest);
    }

    [Fact]
    public void BuildGotoRequest_formats_az_el_invariant()
    {
        Assert.Equal("{\"GOTO\":[35.4,100.5]}", UrcJsonCodec.BuildGotoRequest(35.4, 100.5));
        Assert.Equal("{\"GOTO\":[160,45]}", UrcJsonCodec.BuildGotoRequest(160, 45));
    }

    [Fact]
    public void TryParsePosition_reads_full_status_sample()
    {
        const string json = """
            {
            "TICK":143,
            "UPTIME":53,
            "CPULOAD":15.4,
            "VERSION":1.08,
            "MODE":0,
            "AZ":160.02,
            "EL":89.98,
            "NEWAZ":160.02,
            "NEWEL":89.98
            }
            """;

        Assert.True(UrcJsonCodec.TryParsePosition(json, out var az, out var el));
        Assert.Equal(160.02, az, 3);
        Assert.Equal(89.98, el, 3);
    }

    [Fact]
    public void TryParsePosition_reads_minimal_az_el()
    {
        Assert.True(UrcJsonCodec.TryParsePosition("""{"AZ":12.5,"EL":34}""", out var az, out var el));
        Assert.Equal(12.5, az);
        Assert.Equal(34, el);
    }

    [Fact]
    public void TryParsePosition_accepts_lowercase_keys()
    {
        Assert.True(UrcJsonCodec.TryParsePosition("""{"az":1,"el":2}""", out var az, out var el));
        Assert.Equal(1, az);
        Assert.Equal(2, el);
    }

    [Fact]
    public void TryParsePosition_rejects_garbage()
    {
        Assert.False(UrcJsonCodec.TryParsePosition("", out _, out _));
        Assert.False(UrcJsonCodec.TryParsePosition("not json", out _, out _));
        Assert.False(UrcJsonCodec.TryParsePosition("""{"TICK":1}""", out _, out _));
        Assert.False(UrcJsonCodec.TryParsePosition("""{"AZ":10}""", out _, out _));
    }

    [Fact]
    public void TryExtractCompleteObject_reads_first_object_and_leaves_remainder()
    {
        var buffer = new StringBuilder("  {\"AZ\":1,\"EL\":2}{\"AZ\":3,\"EL\":4}");
        Assert.True(UrcJsonCodec.TryExtractCompleteObject(buffer, out var first));
        Assert.Equal("{\"AZ\":1,\"EL\":2}", first);
        Assert.True(UrcJsonCodec.TryExtractCompleteObject(buffer, out var second));
        Assert.Equal("{\"AZ\":3,\"EL\":4}", second);
        Assert.False(UrcJsonCodec.TryExtractCompleteObject(buffer, out _));
    }

    [Fact]
    public void TryExtractCompleteObject_handles_braces_inside_strings()
    {
        var buffer = new StringBuilder("{\"AZ\":1,\"NOTE\":\"a}b\",\"EL\":2}");
        Assert.True(UrcJsonCodec.TryExtractCompleteObject(buffer, out var json));
        Assert.True(UrcJsonCodec.TryParsePosition(json, out var az, out var el));
        Assert.Equal(1, az);
        Assert.Equal(2, el);
    }

    [Fact]
    public void HasConfiguredEndpoint_requires_host_and_port_for_urc()
    {
        var settings = new OscarWatch.Core.Models.RotatorSettings
        {
            Type = OscarWatch.Core.Models.RotatorType.UrcTcp,
            NetworkHost = "",
            NetworkPort = 1111
        };
        Assert.False(settings.HasConfiguredEndpoint);

        settings.NetworkHost = "192.168.1.10";
        Assert.True(settings.HasConfiguredEndpoint);
        Assert.True(settings.UsesNetworkEndpoint);
    }
}
