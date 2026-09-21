using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

public interface IRigDriver : IDisposable
{
    bool IsConnected { get; }
    RigType RigType { get; }
    void Open();
    /// <summary>Read frequency for a specific VFO (selects that VFO first on Icom).</summary>
    long? ReadFrequencyHz(RigVfo vfo);
    bool SetFrequencyHz(long hz);
    void SelectVfo(RigVfo vfo, bool force = false);
    void SetMode(string mode);
    void SetSplitOn(bool on);
    void SetSatelliteMode(bool on);
    void ExchangeVfos();
    void SetToneOn(bool on);
    void SetToneSquelchOn(bool on);
    void SetToneHz(double hz, bool squelchTone);
    bool SupportsTracking { get; }
    /// <summary>
    /// True when rig-specific satellite mode is currently active.
    /// Drivers that do not expose/require explicit satellite-mode state return true.
    /// </summary>
    bool IsSatelliteModeActive => true;
    /// <summary>False when the radio cannot swap VFOs remotely (e.g. FT-847).</summary>
    bool SupportsVfoExchange => true;

    /// <summary>True when <see cref="SetPtt"/> sends a CAT transmit command.</summary>
    bool SupportsCatPtt => false;

    /// <summary>Key or unkey via CAT when <see cref="SupportsCatPtt"/> is true.</summary>
    void SetPtt(bool transmit)
    {
    }

    /// <summary>
    /// Toggle RTS or DTR on the CAT serial port. Returns false when the driver cannot
    /// expose handshake lines (e.g. network Flex, or RTS already used for flow control).
    /// </summary>
    bool TrySetHandshakeLine(bool useRts, bool assert) => false;
}
