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
            _port.DiscardInBuffer();
            _port.Write("C2\r");
            Thread.Sleep(100);
            var response = ReadLineSafe();
            if (TryParsePosition(response, out var az, out var el))
                return (az, el);

            int? azOnly = null;
            int? elOnly = null;

            _port.Write("C\r");
            Thread.Sleep(100);
            if (TryParseAzimuth(ReadLineSafe(), out var azParsed))
                azOnly = azParsed;

            _port.Write("B\r");
            Thread.Sleep(100);
            if (TryParseElevation(ReadLineSafe(), out var elParsed))
                elOnly = elParsed;

            if (azOnly is not null && elOnly is not null)
                return (azOnly, elOnly);

            return (null, null);
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

    private string ReadLineSafe()
    {
        try
        {
            return _port.ReadLine().Trim();
        }
        catch
        {
            return "";
        }
    }

    private static bool TryParsePosition(string response, out int azimuth, out int elevation)
    {
        azimuth = 0;
        elevation = 0;
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var azFound = false;
        var elFound = false;
        foreach (var part in response.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseAzimuth(part, out azimuth))
                azFound = true;
            else if (TryParseElevation(part, out elevation))
                elFound = true;
        }

        return azFound && elFound;
    }

    private static bool TryParseAzimuth(string token, out int azimuth)
    {
        azimuth = 0;
        if (!token.StartsWith("AZ=", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(token.AsSpan(3), out azimuth);
    }

    private static bool TryParseElevation(string token, out int elevation)
    {
        elevation = 0;
        if (!token.StartsWith("EL=", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(token.AsSpan(3), out elevation);
    }
}
