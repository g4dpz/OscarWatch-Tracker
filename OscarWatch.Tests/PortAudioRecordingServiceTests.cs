using OscarWatch.Recording;

namespace OscarWatch.Tests;

public sealed class PortAudioRecordingServiceTests
{
    [Fact]
    public void IsAvailable_is_provisional_before_TryInitialize()
    {
        using var service = new PortAudioRecordingService();

        Assert.True(service.IsAvailable);
        Assert.Null(service.UnavailableReason);
    }

    [Fact]
    public void TryInitialize_reports_availability_without_throwing()
    {
        using var service = new PortAudioRecordingService();

        var initialized = service.TryInitialize();

        Assert.Equal(initialized, service.IsAvailable);
        if (!initialized)
            Assert.False(string.IsNullOrWhiteSpace(service.UnavailableReason));
        else
            Assert.Null(service.UnavailableReason);
    }
}
