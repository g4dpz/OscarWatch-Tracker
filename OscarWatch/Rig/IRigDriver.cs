using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public interface IRigDriver : IDisposable
{
    bool IsConnected { get; }
    RigType RigType { get; }
    void Open();
    long? GetFrequencyHz();
    bool SetFrequencyHz(long hz);
    void SelectVfo(RigVfo vfo);
    void SetMode(string mode);
    void SetSplitOn(bool on);
    void SetSatelliteMode(bool on);
    void ExchangeVfos();
    void SetToneOn(bool on);
    void SetToneSquelchOn(bool on);
    void SetToneHz(double hz, bool squelchTone);
    bool SupportsTracking { get; }
}
