using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>
/// Downlink driver for SDR applications exposing a Hamlib-style rigctl TCP server (SDR++, SDR Connect).
/// </summary>
public sealed class RigCtlTcpDriver : IRigDriver
{
    private readonly RigCtlTcpClient _client;

    public RigCtlTcpDriver(string host, int port, int catDelayMs = 50)
    {
        _ = catDelayMs;
        _client = new RigCtlTcpClient(host, port);
    }

    internal RigCtlTcpDriver(RigCtlTcpClient client)
    {
        _client = client;
    }

    public RigType RigType => RigType.SdrRigCtlTcp;
    public bool IsConnected => _client.IsConnected;
    public bool SupportsTracking => true;

    public void Open() => _client.Open();

    public long? ReadFrequencyHz(RigVfo vfo)
    {
        if (vfo is not (RigVfo.Main or RigVfo.VfoA))
            return null;

        try
        {
            return _client.ReadFrequencyHz();
        }
        catch
        {
            return null;
        }
    }

    public bool SetFrequencyHz(long hz) => _client.SetFrequencyHz(hz);

    public void SelectVfo(RigVfo vfo, bool force = false)
    {
    }

    public void SetMode(string mode)
    {
        var hamlibMode = RigCtlModeMapper.ToHamlibMode(mode);
        if (hamlibMode is null)
            return;

        _client.SetMode(hamlibMode);
    }

    public void SetSplitOn(bool on)
    {
    }

    public void SetSatelliteMode(bool on)
    {
    }

    public void ExchangeVfos()
    {
    }

    public void SetToneOn(bool on)
    {
    }

    public void SetToneSquelchOn(bool on)
    {
    }

    public void SetToneHz(double hz, bool squelchTone)
    {
    }

    public void Dispose() => _client.Dispose();
}
