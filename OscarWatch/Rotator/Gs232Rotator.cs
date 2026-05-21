using System.IO.Ports;
using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

public sealed class Gs232Rotator : IRotatorDriver
{
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Gs232Rotator(string portName, int baudRate)
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
        var az = (int)Math.Clamp(Math.Round(azimuthDeg), 0, (int)settings.MaxAzimuthDeg);
        var el = (int)Math.Clamp(Math.Round(elevationDeg), 0, (int)settings.MaxElevationDeg);
        SendCommand($"W{az:000} {el:000}");
    }

    public void Stop() => SendCommand("S");

    public (int? Azimuth, int? Elevation) GetPosition()
    {
        _gate.Wait();
        try
        {
            // C2: 'AZ=aaa EL=eee', or just 'AZ=aaa', or just 'EL=eee' — then C / B for missing axis.
            Gs232PositionParser.TryParseParts(Query("C2"), out var az, out var el);

            if (az is null)
            {
                if (Gs232PositionParser.TryParseAzimuthLine(Query("C"), out var azValue))
                    az = azValue;
            }

            if (el is null)
            {
                if (Gs232PositionParser.TryParseElevationLine(Query("B"), out var elValue))
                    el = elValue;
            }

            return az is not null && el is not null ? (az, el) : (null, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            if (_port.IsOpen)
            {
                try { Stop(); } catch { /* ignore */ }
                _port.Close();
            }
        }
        catch
        {
            // ignore dispose errors
        }

        _port.Dispose();
        _gate.Dispose();
    }

    private string? Query(string command)
    {
        _port.DiscardInBuffer();
        _port.Write(command + "\r");
        return ReadLineResponse();
    }

    private void SendCommand(string command)
    {
        _gate.Wait();
        try
        {
            _port.DiscardInBuffer();
            _port.Write(command + "\r");
            Thread.Sleep(150);
            _port.DiscardInBuffer();
        }
        finally
        {
            _gate.Release();
        }
    }

    private string? ReadLineResponse()
    {
        try
        {
            Thread.Sleep(150);
            var line = _port.ReadLine().Trim();
            return string.IsNullOrWhiteSpace(line) ? null : line;
        }
        catch (TimeoutException)
        {
            return ReadExistingLine();
        }
        catch
        {
            return null;
        }
    }

    private string? ReadExistingLine()
    {
        try
        {
            var text = _port.ReadExisting().Trim('\r', '\n', ' ');
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
