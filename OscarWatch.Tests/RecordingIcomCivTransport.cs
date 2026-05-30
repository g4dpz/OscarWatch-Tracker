using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

internal sealed class RecordingIcomCivTransport : IIcomCivTransport
{
    public long MainHz { get; set; } = 435_750_000;
    public Queue<byte[]> SetFrequencyResponses { get; } = new();
    public Queue<byte[]> CommandResponses { get; } = new();
    public byte[]? NextReadResponse { get; set; }
    public int SetFrequencyCommandCount { get; private set; }
    public int CommandCount { get; private set; }
    public List<string> SentCommandBodies { get; } = [];
    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public byte[] WriteCommand(ReadOnlySpan<byte> body, int postDelayMs = 50)
    {
        if (body.Length == 0)
            return [];

        CommandCount++;
        SentCommandBodies.Add(Convert.ToHexString(body).ToLowerInvariant());

        if (CommandResponses.Count > 0)
            return CommandResponses.Dequeue();

        return body[0] switch
        {
            0x05 => HandleSetFrequency(body),
            0x03 => NextReadResponse ?? BuildReadResponse(MainHz),
            0x07 => [0xFE, 0xFE, 0x60, 0x00, 0xFB, 0xFD],
            _ => [0xFE, 0xFE, 0x60, 0x00, 0xFB, 0xFD]
        };
    }

    private byte[] HandleSetFrequency(ReadOnlySpan<byte> body)
    {
        SetFrequencyCommandCount++;

        if (SetFrequencyResponses.Count > 0)
        {
            var custom = SetFrequencyResponses.Dequeue();
            if (custom.Length > 0 && custom.Contains((byte)0xFB)
                && TryDecodeSetFrequencyBody(body, out var hz))
                MainHz = hz;

            return custom;
        }

        if (TryDecodeSetFrequencyBody(body, out var targetHz))
            MainHz = targetHz;

        return BuildSetAck(body);
    }

    private static bool TryDecodeSetFrequencyBody(ReadOnlySpan<byte> body, out long hz)
    {
        hz = 0;
        if (body.Length < 6)
            return false;

        var digits = "";
        for (var i = 5; i >= 1; i--)
            digits += body[i].ToString("X2");

        digits = digits.TrimStart('0');
        if (digits.Length == 0)
            return true;

        return long.TryParse(digits, out hz);
    }

    private static byte[] BuildSetAck(ReadOnlySpan<byte> body) =>
        body.Length >= 6
            ? [0xFE, 0xFE, 0x60, 0x00, 0x00, body[1], body[2], body[3], body[4], body[5], 0xFB, 0xFD]
            : [0xFE, 0xFE, 0x60, 0x00, 0xFB, 0xFD];

    private static byte[] BuildReadResponse(long hz)
    {
        var body = IcomCivCodec.EncodeSetFrequencyHz(hz);
        return [0xFE, 0xFE, 0x60, 0x00, 0x00, body[1], body[2], body[3], body[4], body[5], 0xFB, 0xFD];
    }

    public void Dispose() => IsOpen = false;
}
