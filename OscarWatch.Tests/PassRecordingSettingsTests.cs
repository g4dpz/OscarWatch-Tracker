using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class PassRecordingSettingsTests
{
    [Fact]
    public void MigrateLegacyNumericDeviceId_ClearsIndex_KeepsDisplayName()
    {
        var settings = new PassRecordingSettings
        {
            DeviceId = "5",
            DeviceDisplayName = "USB Audio CODEC"
        };

        settings.MigrateLegacyNumericDeviceId();

        Assert.Equal("", settings.DeviceId);
        Assert.Equal("USB Audio CODEC", settings.DeviceDisplayName);
    }

    [Fact]
    public void MigrateLegacyNumericDeviceId_LeavesRawNameUnchanged()
    {
        var settings = new PassRecordingSettings
        {
            DeviceId = "USB Audio CODEC",
            DeviceDisplayName = "USB Audio CODEC"
        };

        settings.MigrateLegacyNumericDeviceId();

        Assert.Equal("USB Audio CODEC", settings.DeviceId);
    }
}
