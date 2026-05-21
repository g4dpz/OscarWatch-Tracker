using System.Globalization;
using System.IO.Ports;
using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

/// <summary>EasyComm II rotator driver (combined AZ/EL commands).</summary>
public sealed class EasyCommRotator : IRotatorDriver
{
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public EasyCommRotator(string portName, int baudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            NewLine = "\r"
        };
    }

    public void Open() => _port.Open();

    public void SetPosition(double azimuthDeg, double elevationDeg, RotatorSettings settings)
    {
        var az = Math.Clamp(azimuthDeg, 0, settings.MaxAzimuthDeg);
        var el = Math.Clamp(elevationDeg, 0, settings.MaxElevationDeg);
        var azText = az.ToString("000.0", CultureInfo.InvariantCulture);
        var elText = el.ToString("00.0", CultureInfo.InvariantCulture);
        SendCommand($"AZ{azText} EL{elText}");
    }

    public void Stop()
    {
        // EasyComm has no standard stop command.
    }

    public (int? Azimuth, int? Elevation) GetPosition()
    {
        var az = QueryAngle("AZ");
        var el = QueryAngle("EL");
        return (az, el);
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

    private int? QueryAngle(string axis)
    {
        _gate.Wait();
        try
        {
            _port.DiscardInBuffer();
            _port.Write(axis + "\r");
            Thread.Sleep(100);
            return ParseAngle(_port.ReadLine(), axis);
        }
        catch
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void SendCommand(string command)
    {
        _gate.Wait();
        try
        {
            _port.Write(command + "\r");
            Thread.Sleep(100);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static int? ParseAngle(string response, string axis)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        var token = response.Trim();
        if (token.StartsWith(axis, StringComparison.OrdinalIgnoreCase))
            token = token[axis.Length..].Trim();

        if (token.EndsWith(';'))
            token = token[..^1];

        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle)
            && !double.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out angle))
            return null;

        return (int)Math.Round(angle);
    }
}
