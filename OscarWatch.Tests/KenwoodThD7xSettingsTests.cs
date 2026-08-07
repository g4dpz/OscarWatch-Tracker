using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class KenwoodThD7xSettingsTests
{
    [Theory]
    [InlineData(RigType.KenwoodThD74)]
    [InlineData(RigType.KenwoodThD75)]
    public void Ht_is_a_dual_capable_serial_endpoint(RigType type)
    {
        Assert.True(RigSettings.IsKenwoodThD7xEndpoint(type));
        Assert.True(RigSettings.IsDualCapableSerialEndpoint(type));
        Assert.True(new RigEndpointSettings { Type = type, Port = "COM7" }.IsConfigured);
    }
}
