using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

internal sealed class RecordingRigDriver : IRigDriver
{
    public long MainHz { get; set; }
    public long SubHz { get; set; }
    public int SetFrequencyCallCount { get; private set; }
    public double? LastToneHz { get; private set; }
    public bool? LastToneSquelch { get; private set; }
    public bool ToneSquelchOn { get; private set; }
    public bool ToneOn { get; private set; }

    public RigType RigType => RigType.Dummy;
    public bool IsConnected => true;
    public bool SupportsTracking => true;

    public void Open() { }

    public long? ReadFrequencyHz(RigVfo vfo) =>
        vfo is RigVfo.VfoA or RigVfo.Main ? MainHz : SubHz;

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

    public RigVfo? LastToneVfo { get; private set; }

    public void SelectVfo(RigVfo vfo, bool force = false) => _currentVfo = vfo;

    public RigVfo? LastModeVfo { get; private set; }
    public int ModeSetCount { get; private set; }

    public void SetMode(string mode)
    {
        ModeSetCount++;
        LastModeVfo = _currentVfo;
    }
    public void SetSplitOn(bool on) { }
    public int SetSatelliteModeCallCount { get; private set; }
    public bool? LastSatelliteModeOn { get; private set; }

    public void SetSatelliteMode(bool on)
    {
        SetSatelliteModeCallCount++;
        LastSatelliteModeOn = on;
    }
    public int ExchangeVfoCallCount { get; private set; }

    public void ExchangeVfos()
    {
        ExchangeVfoCallCount++;
        (MainHz, SubHz) = (SubHz, MainHz);
    }
    public RigVfo? LastToneOffVfo { get; private set; }

    public void SetToneOn(bool on)
    {
        if (!on)
            LastToneOffVfo = _currentVfo;
        ToneOn = on;
    }

    public void SetToneSquelchOn(bool on)
    {
        if (!on)
            LastToneOffVfo = _currentVfo;
        ToneSquelchOn = on;
    }

    public void SetToneHz(double hz, bool squelchTone)
    {
        LastToneHz = hz;
        LastToneSquelch = squelchTone;
        LastToneVfo = _currentVfo;
    }
    public void Dispose() { }
}
