namespace OscarWatch.Core.Radio;

/// <summary>CI-V frame encoding/decoding (ICOM CAT).</summary>
public static class IcomCivCodec
{
    public static byte[] BuildCommandFrame(int civAddress, ReadOnlySpan<byte> commandBody)
    {
        var frame = new byte[4 + commandBody.Length + 1];
        frame[0] = 0xFE;
        frame[1] = 0xFE;
        frame[2] = (byte)civAddress;
        frame[3] = 0x00;
        commandBody.CopyTo(frame.AsSpan(4));
        frame[^1] = 0xFD;
        return frame;
    }

    public static byte[] EncodeSetFrequencyHz(long hz)
    {
        var s = hz.ToString().PadLeft(10, '0');
        if (s.Length > 10)
            s = s[^10..];
        return
        [
            0x05,
            Convert.ToByte(s[8..10], 16),
            Convert.ToByte(s[6..8], 16),
            Convert.ToByte(s[4..6], 16),
            Convert.ToByte(s[2..4], 16),
            Convert.ToByte(s[0..2], 16)
        ];
    }

    public static long? DecodeFrequencyFromResponse(ReadOnlySpan<byte> response)
    {
        if (response.Length < 10)
            return null;

        var freqBytes = response.Length >= 11 ? response.Slice(5, 5) : response[^6..^1];
        if (freqBytes.Length < 5)
            return null;

        var hex = "";
        for (var i = freqBytes.Length - 1; i >= 0; i--)
            hex += freqBytes[i].ToString("X2");

        if (hex.Length > 0 && hex[0] == '0')
            hex = hex[1..];

        return long.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var hz)
            ? hz
            : null;
    }

    public static int ParseCivAddressHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return 0x60;
        var s = hex.Trim().TrimStart('0', 'x', 'X');
        return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var addr)
            ? addr
            : 0x60;
    }

    public static byte[] EncodeToneHz(double hz, bool squelchTone)
    {
        var hertz = ((int)Math.Round(hz * 10)).ToString();
        if (int.Parse(hertz) >= 1000)
            return [(byte)(squelchTone ? 0x1B : 0x1B), (byte)(squelchTone ? 0x01 : 0x00),
                Convert.ToByte("1" + hertz[1], 16), Convert.ToByte(hertz[2..4], 16)];
        return [(byte)(squelchTone ? 0x1B : 0x1B), (byte)(squelchTone ? 0x01 : 0x00),
            Convert.ToByte("0" + hertz[0], 16), Convert.ToByte(hertz[1..3], 16)];
    }
}
