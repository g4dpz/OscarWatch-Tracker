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
        Assert.Equal("Line In (USB Audio)", devices[0].Id);
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
        Assert.Contains(devices, device => device.DisplayName == "Microphone" && device.Id == "Microphone");
        Assert.Contains(devices, device => device.DisplayName == "Line In" && device.Id == "Line In");
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
        Assert.Equal("radio input", devices[0].Id);
    }

    [Fact]
    public void Build_FormatsDeviceNamesForDisplay_ButIdRemainsRaw()
    {
        var rawMic = "Microphone (@System32\\drivers\\bthhefenum.sys,#2;%1 Hands-Free%0 ;(WF-C700N))";
        var rawIc = "IC-910 (Main) (USB Audio CODEC";
        var devices = RecordingDeviceListBuilder.Build(
        [
            new RecordingDeviceCandidate(0, rawMic, 0.003),
            new RecordingDeviceCandidate(1, rawIc, 0.005)
        ]);

        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, device => device.DisplayName == "WF-C700N" && device.Id == rawMic);
        Assert.Contains(devices, device =>
            device.DisplayName == "IC-910 (Main) (USB Audio CODEC)" && device.Id == rawIc);
    }

    [Fact]
    public void Build_DeduplicatesAfterFormatting_UsesWinningRawNameAsId()
    {
        var rawA = "Device A (@System32\\drivers\\...; (Radio Input))";
        var rawB = "Device B (@System32\\drivers\\...; (Radio Input))";
        var devices = RecordingDeviceListBuilder.Build(
        [
            new RecordingDeviceCandidate(2, rawA, 0.090),
            new RecordingDeviceCandidate(5, rawB, 0.003)
        ]);

        Assert.Single(devices);
        Assert.Equal(rawB, devices[0].Id);
        Assert.Equal("Radio Input", devices[0].DisplayName);
    }
}
