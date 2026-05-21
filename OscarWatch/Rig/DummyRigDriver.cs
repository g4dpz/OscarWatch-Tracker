using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public sealed class DummyRigDriver : IRigDriver
{
    private RigVfo _currentVfo = RigVfo.VfoA;
    private long _vfoA;
    private long _vfoB;

    public RigType RigType => RigType.Dummy;
    public bool IsConnected => true;
    public bool SupportsTracking => true;

    public void Open() { }

    public long? ReadFrequencyHz(RigVfo vfo) =>
        vfo is RigVfo.VfoA or RigVfo.Main ? _vfoA : _vfoB;

    public bool SetFrequencyHz(long hz)
    {
        if (_currentVfo is RigVfo.VfoA or RigVfo.Main)
            _vfoA = hz;
        else
            _vfoB = hz;
        return true;
    }

    public void SelectVfo(RigVfo vfo) => _currentVfo = vfo;
    public void SetMode(string mode) { }
    public void SetSplitOn(bool on) { }
    public void SetSatelliteMode(bool on) { }
    public void ExchangeVfos() => _currentVfo = _currentVfo is RigVfo.VfoA or RigVfo.Main ? RigVfo.VfoB : RigVfo.VfoA;
    public void SetToneOn(bool on) { }
    public void SetToneSquelchOn(bool on) { }
    public void SetToneHz(double hz, bool squelchTone) { }
    public void Dispose() { }
}
