using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

internal sealed class RecordingRigDriver : IRigDriver
{
    public long MainHz { get; private set; }
    public long SubHz { get; private set; }
    public int SetFrequencyCallCount { get; private set; }
    public double? LastToneHz { get; private set; }
    public bool? LastToneSquelch { get; private set; }
    public bool ToneSquelchOn { get; private set; }
    public bool ToneOn { get; private set; }

    public RigType RigType => RigType.Dummy;
    public bool IsConnected => true;
    public bool SupportsTracking => true;

    public void Open() { }

    public long? GetFrequencyHz() => MainHz;

    public bool SetFrequencyHz(long hz)
    {
        SetFrequencyCallCount++;
        if (_currentVfo is RigVfo.VfoA or RigVfo.Main)
            MainHz = hz;
        else
            SubHz = hz;
        return true;
    }

    private RigVfo _currentVfo = RigVfo.Main;

    public RigVfo CurrentVfo => _currentVfo;

    public void SelectVfo(RigVfo vfo) => _currentVfo = vfo;

    public void SetMode(string mode) { }
    public void SetSplitOn(bool on) { }
    public void SetSatelliteMode(bool on) { }
    public void ExchangeVfos() { }
    public void SetToneOn(bool on) => ToneOn = on;

    public void SetToneSquelchOn(bool on) => ToneSquelchOn = on;

    public void SetToneHz(double hz, bool squelchTone)
    {
        LastToneHz = hz;
        LastToneSquelch = squelchTone;
    }
    public void Dispose() { }
}
