using OscarWatch.Core.Models;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class GreenHeronRt21RotatorTests
{
    [Fact]
    public void SetPosition_sends_AP1_to_each_port()
    {
        var az = new RecordingRotatorSerialTransport();
        var el = new RecordingRotatorSerialTransport();
        az.Open();
        el.Open();
        using var driver = new GreenHeronRt21Rotator(az, el);
        var settings = new RotatorSettings
        {
            Type = RotatorType.GreenHeronRt21,
            AzimuthRange = RotatorAzimuthRange.Deg360,
            ElevationRange = RotatorElevationRange.Deg90
        };

        driver.SetPosition(45.2, 30.7, settings);

        Assert.Contains("AP1045.2\r;", az.Written);
        Assert.Contains("AP1030.7\r;", el.Written);
    }

    [Fact]
    public void Stop_sends_semicolon_to_both_ports()
    {
        var az = new RecordingRotatorSerialTransport();
        var el = new RecordingRotatorSerialTransport();
        az.Open();
        el.Open();
        using var driver = new GreenHeronRt21Rotator(az, el);

        driver.Stop();

        Assert.Contains(";", az.Written);
        Assert.Contains(";", el.Written);
    }

    [Fact]
    public void GetPosition_queries_BI1_on_both_ports()
    {
        var az = new RecordingRotatorSerialTransport { NextReadLine = "120.6;" };
        var el = new RecordingRotatorSerialTransport { NextReadLine = " 45.0;" };
        az.Open();
        el.Open();
        using var driver = new GreenHeronRt21Rotator(az, el);

        var (azimuth, elevation) = driver.GetPosition();

        Assert.Equal(121, azimuth);
        Assert.Equal(45, elevation);
        Assert.Contains("BI1;", az.Written);
        Assert.Contains("BI1;", el.Written);
    }

    [Fact]
    public void Open_disposes_azimuth_when_elevation_fails()
    {
        var az = new RecordingRotatorSerialTransport();
        var el = new RecordingRotatorSerialTransport { FailOnOpen = true };
        using var driver = new GreenHeronRt21Rotator(az, el);

        Assert.Throws<InvalidOperationException>(() => driver.Open());
        Assert.True(az.IsDisposed);
        Assert.False(az.IsOpen);
    }

    [Fact]
    public void Factory_creates_green_heron_driver()
    {
        var settings = new RotatorSettings
        {
            Type = RotatorType.GreenHeronRt21,
            Port = "COM3",
            ElevationPort = "COM4",
            BaudRate = 4800
        };

        using var driver = RotatorDriverFactory.Create(settings);
        Assert.IsType<GreenHeronRt21Rotator>(driver);
    }
}

internal sealed class RecordingRotatorSerialTransport : IRotatorSerialTransport
{
    public List<string> Written { get; } = [];
    public string? NextReadLine { get; set; }
    public bool FailOnOpen { get; set; }
    public bool IsDisposed { get; private set; }
    public bool IsOpen { get; private set; }
    public bool DtrEnable { get; set; }
    public bool RtsEnable { get; set; }

    public void Open()
    {
        if (FailOnOpen)
            throw new InvalidOperationException("Simulated open failure");
        IsOpen = true;
    }

    public void Write(string text) => Written.Add(text);

    public void Write(byte[] buffer, int offset, int count) =>
        Written.Add(System.Text.Encoding.ASCII.GetString(buffer, offset, count));

    public void DiscardInBuffer()
    {
    }

    public void DiscardOutBuffer()
    {
    }

    public string ReadLine() => NextReadLine ?? throw new TimeoutException();

    public string ReadExisting() => NextReadLine ?? "";

    public int Read(byte[] buffer, int offset, int count) => 0;

    public void Dispose()
    {
        IsOpen = false;
        IsDisposed = true;
    }
}
