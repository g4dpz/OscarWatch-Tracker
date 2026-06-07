using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Shared setup/teardown and helper methods for all TS-2000 validation tests.
/// Provides transport and driver construction, command log assertions, and resource cleanup.
/// </summary>
public abstract class Ts2000TestBase : IDisposable
{
    private protected IKenwoodCatTransport Transport { get; }
    private protected KenwoodTs2000Driver Driver { get; }
    private protected RecordingKenwoodCatTransport? RecordingTransport { get; }
    protected bool IsHardwareTest { get; }

    protected Ts2000TestBase(bool requireHardware = false)
    {
        IsHardwareTest = requireHardware;

        if (requireHardware)
        {
            Transport = Ts2000TransportFactory.CreateSerialTransport();
            RecordingTransport = null;
            Driver = new KenwoodTs2000Driver(
                Transport,
                catDelayMs: Ts2000TransportFactory.CatDelayMs,
                satModeSettlingDelayMs: 250,
                satModeRetryCount: 3,
                satModeRetryDelayMs: 200);
        }
        else
        {
            var recording = Ts2000TransportFactory.CreateRecordingTransport();
            Transport = recording;
            RecordingTransport = recording;
            Driver = new KenwoodTs2000Driver(
                Transport,
                catDelayMs: 0,
                satModeSettlingDelayMs: 0,
                satModeRetryCount: 3,
                satModeRetryDelayMs: 0);
        }

        Driver.Open();
    }

    protected void EnterSatelliteMode() => Driver.SetSatelliteMode(true);

    protected void ClearCommandLog() => RecordingTransport?.SentCommands.Clear();

    protected IReadOnlyList<string> GetSentCommands() =>
        RecordingTransport?.SentCommands ?? (IReadOnlyList<string>)Array.Empty<string>();

    protected void AssertCommandSequence(params string[] expected) =>
        Assert.Equal(expected, GetSentCommands());

    protected void AssertCommandContains(string command) =>
        Assert.Contains(command, GetSentCommands());

    protected void AssertCommandCount(string command, int expectedCount) =>
        Assert.Equal(expectedCount, GetSentCommands().Count(c => c == command));

    protected void AssertNoCommandStartingWith(string prefix) =>
        Assert.DoesNotContain(GetSentCommands(), c => c.StartsWith(prefix, StringComparison.Ordinal));

    public void Dispose()
    {
        Transport.Dispose();
        GC.SuppressFinalize(this);
    }
}
