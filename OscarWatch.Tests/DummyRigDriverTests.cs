using OscarWatch.Rig;

namespace OscarWatch.Tests;

public class DummyRigDriverTests
{
    [Fact]
    public void SetFrequency_updates_in_memory()
    {
        using var rig = new DummyRigDriver();
        rig.Open();
        rig.SelectVfo(RigVfo.VfoA);
        rig.SetFrequencyHz(145_950_000);
        Assert.Equal(145_950_000, rig.GetFrequencyHz());
    }
}
