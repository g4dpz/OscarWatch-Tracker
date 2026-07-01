using OscarWatch.Recording;

namespace OscarWatch.Tests;

public sealed class RecordingDeviceListBuilderTests
{
    [Fact]
    public void Build_PrefersLowerLatencyDuplicateForSameDeviceName()
    {
        var devices = RecordingDeviceListBuilder.Build(
        [
            new RecordingDeviceCandidate(2, "Line In (USB Audio)", 0.090),
            new RecordingDeviceCandidate(5, "Line In (USB Audio)", 0.003),
            new RecordingDeviceCandidate(8, "Line In (USB Audio)", 0.012)
        ]);

        Assert.Single(devices);
        Assert.Equal("5", devices[0].Id);
        // Display name is formatted (but this one doesn't need formatting)
        Assert.Equal("Line In (USB Audio)", devices[0].DisplayName);
    }

    [Fact]
    public void Build_KeepsDistinctDeviceNames()
    {
        var devices = RecordingDeviceListBuilder.Build(
        [
            new RecordingDeviceCandidate(0, "Microphone", 0.003),
            new RecordingDeviceCandidate(1, "Line In", 0.003),
            new RecordingDeviceCandidate(2, "Microphone", 0.090)
        ]);

        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, device => device.DisplayName == "Microphone" && device.Id == "0");
        Assert.Contains(devices, device => device.DisplayName == "Line In" && device.Id == "1");
    }

    [Fact]
    public void Build_IgnoresNameCaseAndWhitespaceWhenDeduplicating()
    {
        var devices = RecordingDeviceListBuilder.Build(
        [
            new RecordingDeviceCandidate(1, "  Radio Input  ", 0.090),
            new RecordingDeviceCandidate(4, "radio input", 0.003)
        ]);

        Assert.Single(devices);
        Assert.Equal("4", devices[0].Id);
    }

    [Fact]
    public void Build_FormatsDeviceNamesForDisplay()
    {
        var devices = RecordingDeviceListBuilder.Build(
        [
            new RecordingDeviceCandidate(0, "Microphone (@System32\\drivers\\bthhefenum.sys,#2;%1 Hands-Free%0 ;(WF-C700N))", 0.003),
            new RecordingDeviceCandidate(1, "IC-910 (Main) (USB Audio CODEC", 0.005)
        ]);

        Assert.Equal(2, devices.Count);
        // Display names are formatted
        Assert.Contains(devices, device => device.DisplayName == "WF-C700N" && device.Id == "0");
        Assert.Contains(devices, device => device.DisplayName == "IC-910 (Main) (USB Audio CODEC)" && device.Id == "1");
    }

    [Fact]
    public void Build_DeduplicatesAfterFormatting()
    {
        // Two devices with different raw names that format to the same display name
        var devices = RecordingDeviceListBuilder.Build(
        [
            new RecordingDeviceCandidate(2, "Device A (@System32\\drivers\\...; (Radio Input))", 0.090),
            new RecordingDeviceCandidate(5, "Device B (@System32\\drivers\\...; (Radio Input))", 0.003)
        ]);

        // Should keep only the lower-latency device (index 5)
        Assert.Single(devices);
        Assert.Equal("5", devices[0].Id);
        Assert.Equal("Radio Input", devices[0].DisplayName);
    }
}
