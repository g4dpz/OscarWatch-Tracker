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
}
