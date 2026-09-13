using OscarWatch.Core.Models;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class SpidRotatorTests
{
    [Fact]
    public void Md01_SetPosition_consumes_twelve_byte_reply_before_GetPosition()
    {
        var transport = new QueuedRotatorSerialTransport();
        // Open Stop reply (also detects Rot2 variant), SetPosition reply, GetPosition reply
        transport.EnqueueRead(BuildRot2Status(10, 5, pulses: 2));
        transport.EnqueueRead(BuildRot2Status(45, 30, pulses: 2));
        transport.EnqueueRead(BuildRot2Status(45, 30, pulses: 2));

        using var driver = new SpidRotator(transport, expectSetPositionResponse: true);
        driver.Open();

        var settings = new RotatorSettings
        {
            Type = RotatorType.SpidMd01,
            AzimuthRange = RotatorAzimuthRange.Deg450,
            ElevationRange = RotatorElevationRange.Deg180
        };

        driver.SetPosition(45, 30, settings);
        var (az, el) = driver.GetPosition();

        Assert.Equal(45, az);
        Assert.Equal(30, el);
        Assert.Equal(0, transport.UnreadByteCount);
        Assert.True(transport.WriteCount >= 3);
    }

    [Fact]
    public void Classic_SetPosition_does_not_wait_for_reply()
    {
        var transport = new QueuedRotatorSerialTransport();
        // Open / variant detect status reply only
        transport.EnqueueRead(BuildRot2Status(12, 8, pulses: 2));

        using var driver = new SpidRotator(transport, expectSetPositionResponse: false);
        driver.Open();

        var settings = new RotatorSettings
        {
            Type = RotatorType.Spid,
            AzimuthRange = RotatorAzimuthRange.Deg450,
            ElevationRange = RotatorElevationRange.Deg180
        };

        driver.SetPosition(90, 20, settings);

        Assert.Equal(0, transport.UnreadByteCount);
        Assert.Equal(2, transport.WriteCount);
    }

    [Fact]
    public void Factory_creates_spid_for_md01()
    {
        var settings = new RotatorSettings
        {
            Type = RotatorType.SpidMd01,
            NetworkHost = "192.168.1.50",
            NetworkPort = 23
        };

        using var driver = RotatorDriverFactory.Create(settings);
        Assert.IsType<SpidRotator>(driver);
    }

    private static byte[] BuildRot2Status(double azDeg, double elDeg, int pulses)
    {
        var az = (int)Math.Round((360.0 + azDeg) * 10);
        var el = (int)Math.Round((360.0 + elDeg) * 10);
        return
        [
            SpidRotatorCodec.StartByte,
            (byte)(az / 1000 % 10),
            (byte)(az / 100 % 10),
            (byte)(az / 10 % 10),
            (byte)(az % 10),
            (byte)pulses,
            (byte)(el / 1000 % 10),
            (byte)(el / 100 % 10),
            (byte)(el / 10 % 10),
            (byte)(el % 10),
            (byte)pulses,
            SpidRotatorCodec.EndByte
        ];
    }
}

internal sealed class QueuedRotatorSerialTransport : IRotatorSerialTransport
{
    private readonly Queue<byte[]> _replyFrames = new();
    private readonly Queue<byte> _pending = new();

    public int WriteCount { get; private set; }
    public int UnreadByteCount => _pending.Count + _replyFrames.Sum(f => f.Length);
    public bool IsOpen { get; private set; }
    public bool DtrEnable { get; set; }
    public bool RtsEnable { get; set; }

    public void EnqueueRead(byte[] bytes) => _replyFrames.Enqueue(bytes);

    public void Open() => IsOpen = true;

    public void Write(string text) => ArmNextReply();

    public void Write(byte[] buffer, int offset, int count) => ArmNextReply();

    public void DiscardInBuffer() => _pending.Clear();

    public void DiscardOutBuffer()
    {
    }

    public string ReadLine() => throw new NotSupportedException();

    public string ReadExisting() => "";

    public int Read(byte[] buffer, int offset, int count)
    {
        var copied = 0;
        while (copied < count && _pending.Count > 0)
        {
            buffer[offset + copied] = _pending.Dequeue();
            copied++;
        }

        return copied;
    }

    public void Dispose() => IsOpen = false;

    private void ArmNextReply()
    {
        WriteCount++;
        if (_replyFrames.Count == 0)
            return;

        foreach (var b in _replyFrames.Dequeue())
            _pending.Enqueue(b);
    }
}
