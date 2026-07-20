using System.Globalization;
using System.Text;

namespace OscarWatch.Core.Radio;

/// <summary>
/// Parses FlexRadio SmartSDR UDP discovery payloads (ASCII key=value, optionally wrapped in VITA-49).
/// </summary>
public static class FlexDiscoveryCodec
{
    public const int DefaultDiscoveryPort = 4992;

    /// <summary>VITA-49 discovery class ID trailing bytes (0xFFFF).</summary>
    private static readonly byte[] DiscoveryClassIdSuffix = [0xFF, 0xFF];

    public static bool TryParse(ReadOnlySpan<byte> datagram, out FlexDiscoveredRadio radio)
    {
        radio = default!;
        if (datagram.IsEmpty)
            return false;

        if (!TryExtractAsciiPayload(datagram, out var ascii))
            return false;

        return TryParseAscii(ascii, out radio);
    }

    public static bool TryParseAscii(string ascii, out FlexDiscoveredRadio radio)
    {
        radio = default!;
        if (string.IsNullOrWhiteSpace(ascii))
            return false;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in ascii.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = token.IndexOf('=');
            if (eq <= 0 || eq >= token.Length - 1)
                continue;

            var key = token[..eq];
            var value = token[(eq + 1)..];
            fields[key] = value;
        }

        if (!fields.TryGetValue("ip", out var ip) || string.IsNullOrWhiteSpace(ip))
            return false;

        if (!fields.TryGetValue("port", out var portText)
            || !int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            || port is <= 0 or > 65535)
        {
            port = DefaultDiscoveryPort;
        }

        fields.TryGetValue("serial", out var serial);
        fields.TryGetValue("model", out var model);
        fields.TryGetValue("nickname", out var nickname);
        fields.TryGetValue("callsign", out var callsign);
        fields.TryGetValue("version", out var version);
        fields.TryGetValue("status", out var status);
        fields.TryGetValue("discovery_protocol_version", out var protocolVersion);

        if (string.IsNullOrWhiteSpace(model) && !string.IsNullOrWhiteSpace(serial))
            model = InferModelFromSerial(serial);

        radio = new FlexDiscoveredRadio(
            IpAddress: ip.Trim(),
            Port: port,
            Serial: serial?.Trim() ?? "",
            Model: model?.Trim() ?? "",
            Nickname: nickname?.Trim() ?? "",
            Callsign: callsign?.Trim() ?? "",
            Version: version?.Trim() ?? "",
            Status: status?.Trim() ?? "",
            DiscoveryProtocolVersion: protocolVersion?.Trim() ?? "");
        return true;
    }

    /// <summary>
    /// True when the model string suggests a dual-SCU / full-duplex capable radio
    /// (6600, 6700, dual-SCU 8000-class). Single-SCU models such as 6400 return false.
    /// </summary>
    public static bool LooksDuplexCapable(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        var m = model.Trim().ToUpperInvariant();
        if (m.Contains("6400", StringComparison.Ordinal))
            return false;

        return m.Contains("6600", StringComparison.Ordinal)
            || m.Contains("6700", StringComparison.Ordinal)
            || m.Contains("6500", StringComparison.Ordinal)
            || m.Contains("8600", StringComparison.Ordinal)
            || m.Contains("8400", StringComparison.Ordinal)
            || m.Contains("FLEX-8", StringComparison.Ordinal);
    }

    public static string FormatDisplayName(FlexDiscoveredRadio radio)
    {
        var name = !string.IsNullOrWhiteSpace(radio.Nickname)
            ? radio.Nickname
            : !string.IsNullOrWhiteSpace(radio.Model)
                ? radio.Model
                : "FlexRadio";

        var model = !string.IsNullOrWhiteSpace(radio.Model) && !string.Equals(name, radio.Model, StringComparison.Ordinal)
            ? $" ({radio.Model})"
            : "";

        return $"{name}{model} — {radio.IpAddress}:{radio.Port}";
    }

    private static string InferModelFromSerial(string serial)
    {
        // serial=3615-5017-6500-4899 — third group often encodes the model number
        var parts = serial.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[2].Length >= 4)
            return "FLEX-" + parts[2];

        return "";
    }

    private static bool TryExtractAsciiPayload(ReadOnlySpan<byte> datagram, out string ascii)
    {
        ascii = "";

        // Plain ASCII discovery (no VITA wrapper)
        if (LooksLikeAsciiDiscovery(datagram))
        {
            ascii = Encoding.ASCII.GetString(datagram).TrimEnd('\0');
            return true;
        }

        // VITA-49: ASCII key=value starts at payload offset 28 (G3WGV primer)
        if (datagram.Length >= 40)
        {
            var asciiStart = FindAsciiDiscoveryStart(datagram);
            if (asciiStart >= 0)
            {
                ascii = Encoding.ASCII.GetString(datagram[asciiStart..]).TrimEnd('\0');
                return ascii.Contains('=', StringComparison.Ordinal);
            }
        }

        // Fallback: scan for "serial=" anywhere
        var text = Encoding.ASCII.GetString(datagram).TrimEnd('\0');
        if (text.Contains("serial=", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ip=", StringComparison.OrdinalIgnoreCase))
        {
            var idx = text.IndexOf("discovery_protocol", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                idx = text.IndexOf("serial=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                idx = text.IndexOf("ip=", StringComparison.OrdinalIgnoreCase);

            if (idx >= 0)
            {
                ascii = text[idx..];
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeAsciiDiscovery(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < 8)
            return false;

        // Printable ASCII starting with a letter
        var c = datagram[0];
        if (c is < (byte)'A' or > (byte)'z')
            return false;

        var probe = Encoding.ASCII.GetString(datagram[..Math.Min(64, datagram.Length)]);
        return probe.Contains('=', StringComparison.Ordinal)
            && (probe.Contains("ip=", StringComparison.OrdinalIgnoreCase)
                || probe.Contains("serial=", StringComparison.OrdinalIgnoreCase));
    }

    private static int FindAsciiDiscoveryStart(ReadOnlySpan<byte> datagram)
    {
        // Prefer documented VITA offset 28 when class-id suffix matches
        if (datagram.Length > 28 + 8)
        {
            // Class ID at bytes 8..15 of VITA payload; trailing FFFF at 14..15
            if (datagram.Length >= 16
                && datagram[14] == DiscoveryClassIdSuffix[0]
                && datagram[15] == DiscoveryClassIdSuffix[1]
                && LooksLikeAsciiDiscovery(datagram[28..]))
            {
                return 28;
            }
        }

        for (var i = 0; i < datagram.Length - 8; i++)
        {
            if (datagram[i] == (byte)'s'
                && i + 7 <= datagram.Length
                && datagram[i + 1] == (byte)'e'
                && datagram[i + 2] == (byte)'r'
                && datagram[i + 3] == (byte)'i'
                && datagram[i + 4] == (byte)'a'
                && datagram[i + 5] == (byte)'l'
                && datagram[i + 6] == (byte)'=')
            {
                return i;
            }

            if (datagram[i] == (byte)'d'
                && i + 20 < datagram.Length
                && Encoding.ASCII.GetString(datagram.Slice(i, Math.Min(20, datagram.Length - i)))
                    .StartsWith("discovery_protocol", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>One FlexRadio found via UDP discovery on port 4992.</summary>
public sealed record FlexDiscoveredRadio(
    string IpAddress,
    int Port,
    string Serial,
    string Model,
    string Nickname,
    string Callsign,
    string Version,
    string Status,
    string DiscoveryProtocolVersion);
