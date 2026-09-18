using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Xunit;

namespace OscarWatch.Tests;

public sealed class GroundTrackRebuildPriorityTests
{
    [Fact]
    public void Above_horizon_has_higher_priority_than_below()
    {
        var above = new LookAngles(10, 5, 1000);
        var below = new LookAngles(10, -5, 1000);

        Assert.Equal(0, TrackingOrchestrator.GroundTrackRebuildPriority(above));
        Assert.Equal(1, TrackingOrchestrator.GroundTrackRebuildPriority(below));
        Assert.True(TrackingOrchestrator.GroundTrackRebuildPriority(above)
            < TrackingOrchestrator.GroundTrackRebuildPriority(below));
    }

    [Fact]
    public void Missing_look_angles_are_low_priority()
    {
        Assert.Equal(1, TrackingOrchestrator.GroundTrackRebuildPriority(null));
    }

    [Fact]
    public void Horizon_boundary_counts_as_above()
    {
        Assert.Equal(0, TrackingOrchestrator.GroundTrackRebuildPriority(new LookAngles(0, 0, 500)));
    }
}
