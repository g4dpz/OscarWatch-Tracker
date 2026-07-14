using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Shared setup/teardown and helper methods for TS-2000 validation tests against a recording transport.
/// </summary>
public abstract class Ts2000TestBase : IDisposable
{
    private protected IKenwoodCatTransport Transport { get; }
    private protected KenwoodTs2000Driver Driver { get; }
    private protected RecordingKenwoodCatTransport RecordingTransport { get; }

    protected Ts2000TestBase()
    {
        RecordingTransport = Ts2000TransportFactory.CreateRecordingTransport();
        Transport = RecordingTransport;
        Driver = new KenwoodTs2000Driver(
            Transport,
            catDelayMs: 0,
            satModeSettlingDelayMs: 0,
            satModeRetryCount: 3,
            satModeRetryDelayMs: 0);
        Driver.Open();
    }

    protected void EnterSatelliteMode() => Driver.SetSatelliteMode(true);

    protected void ClearCommandLog() => RecordingTransport.SentCommands.Clear();

    protected IReadOnlyList<string> GetSentCommands() => RecordingTransport.SentCommands;

    protected void AssertCommandSequence(params string[] expected) =>
        Assert.Equal(expected, GetSentCommands());

    protected void AssertCommandContains(string command) =>
        Assert.Contains(command, GetSentCommands());

    protected void AssertCommandCount(string command, int expectedCount) =>
        Assert.Equal(expectedCount, GetSentCommands().Count(c => c == command));

    protected void AssertNoCommandStartingWith(string prefix) =>
        Assert.DoesNotContain(GetSentCommands(), c => c.StartsWith(prefix, StringComparison.Ordinal));

    protected int SatelliteEntryStartIndex()
    {
        var cmds = GetSentCommands();
        for (var i = 0; i < cmds.Count; i++)
        {
            if (cmds[i] == "SA1010110;")
                return i;
        }

        Assert.Fail("SA1010110; not found in command log.");
        return -1;
    }

    public void Dispose()
    {
        Transport.Dispose();
        GC.SuppressFinalize(this);
    }
}
