using System.IO.Ports;
using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

/// <summary>
/// SAEBRTrack rotator driver for fire-and-forget Arduino / BASIC Stamp / PSR-100 style interfaces.
/// Sends compact whole-degree <c>AZnnnELnnn</c> gotos; does not query position
/// (bare <c>AZ</c>/<c>EL</c> would be misread as move-to-0/0 by these controllers).
/// </summary>
public sealed class SaebrtRotator : IRotatorDriver
{
    private const string LineEnding = "\n";

    private readonly SerialPort _port;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SaebrtRotator(string portName, int baudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            NewLine = LineEnding
        };
    }

    public void Open() => _port.Open();

    public void SetPosition(double azimuthDeg, double elevationDeg, RotatorSettings settings)
    {
        var az = Math.Clamp(azimuthDeg, 0, settings.MaxAzimuthDeg);
        var el = Math.Clamp(elevationDeg, 0, settings.MaxElevationDeg);
        SendCommand(SaebrtCommandFormatter.FormatSetPosition(az, el));
    }

    public void Stop()
    {
        // SAEBRTrack has no standard stop command; sending SA/SE can confuse dumb controllers.
    }

    public (int? Azimuth, int? Elevation) GetPosition()
    {
        // Fire-and-forget firmware treats every serial read as a goto. Sending EasyComm-style
        // AZ/EL queries parses as az=0/el=0 and drives the rotor back to park.
        return (null, null);
    }

    public void Dispose()
    {
        try
        {
            if (_port.IsOpen)
                _port.Close();
        }
        catch
        {
            // ignore dispose errors
        }

        _port.Dispose();
        _gate.Dispose();
    }

    private void SendCommand(string command)
    {
        _gate.Wait();
        try
        {
            _port.DiscardInBuffer();
            _port.Write(command + LineEnding);
            Thread.Sleep(150);
            _port.DiscardInBuffer();
        }
        finally
        {
            _gate.Release();
        }
    }
}
