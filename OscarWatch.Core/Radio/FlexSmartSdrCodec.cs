using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace OscarWatch.Core.Radio;

/// <summary>SmartSDR TCP/IP command framing (C/R/S/V/H/M lines).</summary>
public static class FlexSmartSdrCodec
{
    public const int DefaultApiPort = 4992;

    // Reusable StringBuilder buffer for command construction to avoid string concatenation allocations
    private static readonly StringBuilder _commandBuffer = new(256);

    public static string BuildCommand(uint sequence, string body, bool debug = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var prefix = debug ? "CD" : "C";
        return $"{prefix}{sequence.ToString(CultureInfo.InvariantCulture)}|{body.Trim()}\n";
    }

    public static string BuildClientProgramCommand(uint sequence, string programName = "OscarWatch") =>
        BuildCommand(sequence, BuildClientProgramBody(programName));

    public static string BuildSubSliceAllCommand(uint sequence) =>
        BuildCommand(sequence, "sub slice all");

    public static string BuildSubRadioAllCommand(uint sequence) =>
        BuildCommand(sequence, "sub radio all");

    public static string BuildSubPanAllCommand(uint sequence) =>
        BuildCommand(sequence, "sub pan all");

    public static string BuildFullDuplexCommand(uint sequence, bool enabled) =>
        BuildCommand(sequence, BuildRadioFullDuplexBody(enabled));

    public static string BuildSliceCreateCommand(
        uint sequence,
        double freqMhz,
        string mode,
        string? ant = null,
        string? panStreamId = null)
    {
        return BuildCommand(sequence, BuildSliceCreateBody(freqMhz, mode, ant, panStreamId));
    }

    public static string BuildSliceRemoveCommand(uint sequence, int sliceIndex) =>
        BuildCommand(sequence, BuildSliceRemoveBody(sliceIndex));

    public static string BuildSliceTuneCommand(
        uint sequence,
        int sliceIndex,
        double freqMhz,
        bool autoPan = false) =>
        BuildCommand(sequence, BuildSliceTuneBody(sliceIndex, freqMhz, autoPan));

    public static string BuildDisplayPanCenterCommand(uint sequence, string panStreamId, double centerMhz) =>
        BuildCommand(sequence, BuildDisplayPanCenterBody(panStreamId, centerMhz));

    /// <summary>Creates a panadapter + waterfall (SmartSDR / AetherSDR wire command).</summary>
    public static string BuildDisplayPanafallCreateCommand(uint sequence) =>
        BuildCommand(sequence, "display panafall create");

    /// <summary>Legacy pan create used by older firmware when panafall create is unavailable.</summary>
    public static string BuildPanadapterCreateCommand(uint sequence) =>
        BuildCommand(sequence, "panadapter create");

    public static string BuildDisplayPanRemoveCommand(uint sequence, string panStreamId) =>
        BuildCommand(sequence, BuildDisplayPanRemoveBody(panStreamId));

    public static string BuildDisplayPanafallRemoveCommand(uint sequence, string panStreamId) =>
        BuildCommand(sequence, BuildDisplayPanafallRemoveBody(panStreamId));

    public static string BuildSliceSetModeCommand(uint sequence, int sliceIndex, string mode) =>
        BuildCommand(sequence, BuildSliceSetModeBody(sliceIndex, mode));

    public static string BuildSliceSetActiveCommand(uint sequence, int sliceIndex, bool active) =>
        BuildCommand(sequence, BuildSliceSetActiveBody(sliceIndex, active));

    /// <summary>Cross-pan click-to-tune: moves the active slice to <paramref name="panStreamId"/>.</summary>
    public static string BuildSliceMoveCommand(uint sequence, double freqMhz, string panStreamId) =>
        BuildCommand(sequence, BuildSliceMoveBody(freqMhz, panStreamId));

    public static string BuildSliceSetTxCommand(uint sequence, int sliceIndex, bool tx) =>
        BuildCommand(sequence, BuildSliceSetTxBody(sliceIndex, tx));

    public static string BuildSliceSetRxAntCommand(uint sequence, int sliceIndex, string antennaPort) =>
        BuildCommand(sequence, BuildSliceSetRxAntBody(sliceIndex, antennaPort));

    public static string BuildSliceSetTxAntCommand(uint sequence, int sliceIndex, string antennaPort) =>
        BuildCommand(sequence, BuildSliceSetTxAntBody(sliceIndex, antennaPort));

    public static string BuildSliceSetToneModeCommand(uint sequence, int sliceIndex, bool toneOn) =>
        BuildCommand(sequence, BuildSliceSetToneModeBody(sliceIndex, toneOn));

    public static string BuildSliceSetToneValueCommand(uint sequence, int sliceIndex, double toneHz) =>
        BuildCommand(sequence, BuildSliceSetToneValueBody(sliceIndex, toneHz));

    public static long MhzToHz(double mhz) =>
        (long)Math.Round(mhz * 1_000_000d, MidpointRounding.AwayFromZero);

    public static double HzToMhz(long hz) => hz / 1_000_000d;

    public static bool TryParseLine(string line, out FlexSmartSdrMessage message)
    {
        message = default!;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (trimmed.Length < 2)
            return false;

        switch (trimmed[0])
        {
            case 'V':
                message = new FlexSmartSdrMessage(
                    FlexSmartSdrMessageKind.Version,
                    Sequence: 0,
                    HexResponse: 0,
                    Body: trimmed[1..],
                    Handle: "");
                return true;

            case 'H':
                message = new FlexSmartSdrMessage(
                    FlexSmartSdrMessageKind.Handle,
                    Sequence: 0,
                    HexResponse: 0,
                    Body: trimmed[1..],
                    Handle: trimmed[1..]);
                return true;

            case 'R':
                return TryParseResponse(trimmed, out message);

            case 'S':
                return TryParseStatus(trimmed, out message);

            case 'M':
                return TryParseRadioMessage(trimmed, out message);

            default:
                return false;
        }
    }

    public static bool IsSuccessResponse(FlexSmartSdrMessage message) =>
        message.Kind == FlexSmartSdrMessageKind.Response && message.HexResponse == 0;

    public static bool TryParseSliceStatus(string statusBody, out FlexSliceState slice)
    {
        slice = default!;
        if (string.IsNullOrWhiteSpace(statusBody))
            return false;

        var body = statusBody.Trim();
        if (!body.StartsWith("slice ", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = body["slice ".Length..].TrimStart();
        var space = rest.IndexOf(' ');
        if (space <= 0)
            return false;

        if (!int.TryParse(rest.AsSpan(0, space), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            return false;

        var fields = ParseKeyValues(rest[(space + 1)..]);
        // Missing in_use must not invent ghost in-use slices from frequency/mode-only status.
        var inUse = GetInt(fields, "in_use", 0) != 0;
        var freqMhz = GetDouble(fields, "RF_frequency", 0);
        if (freqMhz <= 0)
            freqMhz = GetDouble(fields, "freq", 0);

        fields.TryGetValue("mode", out var mode);
        var tx = GetInt(fields, "tx", 0) != 0;
        var active = GetInt(fields, "active", 0) != 0;
        fields.TryGetValue("fm_tone_mode", out var toneMode);
        var toneHz = GetDouble(fields, "fm_tone_value", 0);
        fields.TryGetValue("pan", out var panStreamId);

        slice = new FlexSliceState(
            Index: index,
            InUse: inUse,
            FrequencyHz: MhzToHz(freqMhz),
            Mode: mode ?? "",
            IsTransmit: tx,
            IsActive: active,
            FmToneMode: toneMode ?? "",
            FmToneHz: toneHz,
            PanStreamId: panStreamId ?? "");
        return true;
    }

    public static bool TryParseDisplayPanStatus(string statusBody, out FlexPanState pan)
    {
        pan = default!;
        if (string.IsNullOrWhiteSpace(statusBody))
            return false;

        var body = statusBody.Trim();
        if (!body.StartsWith("display pan ", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = body["display pan ".Length..].TrimStart();
        var space = rest.IndexOf(' ');
        if (space <= 0)
            return false;

        var streamId = rest[..space];
        if (string.IsNullOrWhiteSpace(streamId))
            return false;

        var fields = ParseKeyValues(rest[(space + 1)..]);
        var centerMhz = GetDouble(fields, "center", 0);
        var autoCenter = GetInt(fields, "autocenter", 0) != 0;

        pan = new FlexPanState(
            streamId,
            centerMhz > 0 ? MhzToHz(centerMhz) : 0,
            autoCenter);
        return true;
    }

    public static bool TryParseRadioFullDuplex(string statusBody, out bool enabled)
    {
        enabled = false;
        if (string.IsNullOrWhiteSpace(statusBody)
            || !statusBody.TrimStart().StartsWith("radio ", StringComparison.OrdinalIgnoreCase))
            return false;

        var fields = ParseKeyValues(statusBody);
        if (!fields.TryGetValue("full_duplex_enabled", out var value))
            return false;

        enabled = value is "1" or "true" or "True";
        return true;
    }

    public static bool TryParseSliceCreateIndex(string responseMessage, out int sliceIndex)
    {
        sliceIndex = -1;
        if (string.IsNullOrWhiteSpace(responseMessage))
            return false;

        var trimmed = responseMessage.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out sliceIndex))
            return sliceIndex >= 0;

        // Some firmware replies "slice N" or trailing token
        foreach (var token in trimmed.Split([' ', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out sliceIndex)
                && sliceIndex >= 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Parses the pan stream id from a panafall/panadapter create response body
    /// (<c>pan=0x…</c>, <c>id=0x…</c>, or a bare hex token).
    /// </summary>
    public static bool TryParsePanafallCreatePanId(string responseMessage, out string panStreamId)
    {
        panStreamId = "";
        if (string.IsNullOrWhiteSpace(responseMessage))
            return false;

        var trimmed = responseMessage.Trim();
        foreach (var token in trimmed.Split([' ', '|', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = token.IndexOf('=');
            if (eq > 0
                && eq < token.Length - 1
                && (token.AsSpan(0, eq).Equals("pan", StringComparison.OrdinalIgnoreCase)
                    || token.AsSpan(0, eq).Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                var value = token[(eq + 1)..].Trim();
                if (LooksLikePanStreamId(value))
                {
                    panStreamId = value;
                    return true;
                }
            }

            if (LooksLikePanStreamId(token))
            {
                panStreamId = token;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikePanStreamId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (hex.Length is < 1 or > 16)
            return false;

        foreach (var c in hex)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }

    private static bool TryParseResponse(string line, out FlexSmartSdrMessage message)
    {
        message = default!;
        // R<seq>|<hex>|<message>
        var payload = line[1..];
        var parts = payload.Split('|', 3);
        if (parts.Length < 2)
            return false;

        if (!uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq))
            return false;

        if (!uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            return false;

        var body = parts.Length > 2 ? parts[2] : "";
        message = new FlexSmartSdrMessage(
            FlexSmartSdrMessageKind.Response,
            Sequence: seq,
            HexResponse: hex,
            Body: body,
            Handle: "");
        return true;
    }

    private static bool TryParseStatus(string line, out FlexSmartSdrMessage message)
    {
        message = default!;
        // S<handle>|<message>
        var payload = line[1..];
        var bar = payload.IndexOf('|');
        if (bar <= 0)
            return false;

        message = new FlexSmartSdrMessage(
            FlexSmartSdrMessageKind.Status,
            Sequence: 0,
            HexResponse: 0,
            Body: payload[(bar + 1)..],
            Handle: payload[..bar]);
        return true;
    }

    private static bool TryParseRadioMessage(string line, out FlexSmartSdrMessage message)
    {
        message = default!;
        // M<hex>|<text>
        var payload = line[1..];
        var bar = payload.IndexOf('|');
        if (bar <= 0)
        {
            message = new FlexSmartSdrMessage(
                FlexSmartSdrMessageKind.Message,
                Sequence: 0,
                HexResponse: 0,
                Body: payload,
                Handle: "");
            return true;
        }

        _ = uint.TryParse(payload.AsSpan(0, bar), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex);
        message = new FlexSmartSdrMessage(
            FlexSmartSdrMessageKind.Message,
            Sequence: 0,
            HexResponse: hex,
            Body: payload[(bar + 1)..],
            Handle: "");
        return true;
    }

    private static Dictionary<string, string> ParseKeyValues(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Optimized: Use ReadOnlySpan<char> to avoid Split() array allocations during parsing
        // Note: Still allocates strings for keys/values via ToString(); the win is avoiding Split() array/token allocations
        var span = text.AsSpan();
        var pos = 0;
        
        while (pos < span.Length)
        {
            // Skip whitespace
            while (pos < span.Length && IsWhitespace(span[pos]))
                pos++;
                
            if (pos >= span.Length)
                break;
                
            // Find end of token (next whitespace or end)
            var tokenStart = pos;
            while (pos < span.Length && !IsWhitespace(span[pos]))
                pos++;
                
            var token = span.Slice(tokenStart, pos - tokenStart);
            
            // Find equals sign in token
            var eqIndex = token.IndexOf('=');
            if (eqIndex <= 0 || eqIndex >= token.Length - 1)
                continue;
                
            // Extract key and value using spans, then convert to strings only when storing
            var keySpan = token.Slice(0, eqIndex);
            var valueSpan = token.Slice(eqIndex + 1);
            
            fields[keySpan.ToString()] = valueSpan.ToString();
        }
        
        return fields;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';

    private static int GetInt(Dictionary<string, string> fields, string key, int fallback)
    {
        if (!fields.TryGetValue(key, out var value))
            return fallback;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static double GetDouble(Dictionary<string, string> fields, string key, double fallback)
    {
        if (!fields.TryGetValue(key, out var value))
            return fallback;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static string SanitizeToken(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.IndexOfAny([' ', '|', '\r', '\n']) >= 0)
            throw new ArgumentException("SmartSDR tokens cannot contain spaces or pipes.", nameof(value));

        return trimmed;
    }

    /// <summary>
    /// Builds client program command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 2 string allocations per command.
    /// </summary>
    private static string BuildClientProgramBody(string programName)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("client program ");
            _commandBuffer.Append(SanitizeToken(programName));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds radio full duplex command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildRadioFullDuplexBody(bool enabled)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("radio set full_duplex_enabled=");
            _commandBuffer.Append(enabled ? "1" : "0");
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice create command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 4+ string allocations per command depending on optional parameters.
    /// </summary>
    private static string BuildSliceCreateBody(double freqMhz, string mode, string? ant, string? panStreamId)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice create freq=");
            _commandBuffer.Append(freqMhz.ToString("0.######", CultureInfo.InvariantCulture));
            
            if (!string.IsNullOrWhiteSpace(panStreamId))
            {
                _commandBuffer.Append(" pan=");
                _commandBuffer.Append(SanitizeToken(panStreamId));
            }
            
            if (!string.IsNullOrWhiteSpace(mode))
            {
                _commandBuffer.Append(" mode=");
                _commandBuffer.Append(SanitizeToken(mode));
            }
            
            if (!string.IsNullOrWhiteSpace(ant))
            {
                _commandBuffer.Append(" ant=");
                _commandBuffer.Append(SanitizeToken(ant));
            }
            
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice remove command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 2 string allocations per command.
    /// </summary>
    private static string BuildSliceRemoveBody(int sliceIndex)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice remove ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice tune command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 4 string allocations per command - critical for high-frequency Doppler tracking.
    /// </summary>
    private static string BuildSliceTuneBody(int sliceIndex, double freqMhz, bool autoPan)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice tune ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(' ');
            _commandBuffer.Append(freqMhz.ToString("0.######", CultureInfo.InvariantCulture));
            _commandBuffer.Append(" autopan=");
            _commandBuffer.Append(autoPan ? "1" : "0");
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds display pan center command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 4 string allocations per command.
    /// </summary>
    private static string BuildDisplayPanCenterBody(string panStreamId, double centerMhz)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("display pan set ");
            _commandBuffer.Append(SanitizeToken(panStreamId));
            _commandBuffer.Append(" center=");
            _commandBuffer.Append(centerMhz.ToString("0.######", CultureInfo.InvariantCulture));
            _commandBuffer.Append(" autocenter=0");
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds display pan remove command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 2 string allocations per command.
    /// </summary>
    private static string BuildDisplayPanRemoveBody(string panStreamId)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("display pan remove ");
            _commandBuffer.Append(SanitizeToken(panStreamId));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds display panafall remove command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 2 string allocations per command.
    /// </summary>
    private static string BuildDisplayPanafallRemoveBody(string panStreamId)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("display panafall remove ");
            _commandBuffer.Append(SanitizeToken(panStreamId));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice set mode command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceSetModeBody(int sliceIndex, string mode)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice set ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(" mode=");
            _commandBuffer.Append(SanitizeToken(mode));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice set active command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceSetActiveBody(int sliceIndex, bool active)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice set ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(" active=");
            _commandBuffer.Append(active ? "1" : "0");
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice move command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceMoveBody(double freqMhz, string panStreamId)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice m ");
            _commandBuffer.Append(freqMhz.ToString("0.######", CultureInfo.InvariantCulture));
            _commandBuffer.Append(" pan=");
            _commandBuffer.Append(SanitizeToken(panStreamId));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice set TX command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceSetTxBody(int sliceIndex, bool tx)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice set ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(" tx=");
            _commandBuffer.Append(tx ? "1" : "0");
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice set RX antenna command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceSetRxAntBody(int sliceIndex, string antennaPort)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice set ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(" rxant=");
            _commandBuffer.Append(SanitizeToken(antennaPort));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice set TX antenna command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceSetTxAntBody(int sliceIndex, string antennaPort)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice set ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(" txant=");
            _commandBuffer.Append(SanitizeToken(antennaPort));
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice set tone mode command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceSetToneModeBody(int sliceIndex, bool toneOn)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice s ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(" fm_tone_mode=");
            _commandBuffer.Append(toneOn ? "ctcss_tx" : "off");
            return _commandBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds slice set tone value command body using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 3 string allocations per command.
    /// </summary>
    private static string BuildSliceSetToneValueBody(int sliceIndex, double toneHz)
    {
        lock (_commandBuffer)
        {
            _commandBuffer.Clear();
            _commandBuffer.Append("slice s ");
            _commandBuffer.Append(sliceIndex.ToString(CultureInfo.InvariantCulture));
            _commandBuffer.Append(" fm_tone_value=");
            _commandBuffer.Append(toneHz.ToString("0.0", CultureInfo.InvariantCulture));
            return _commandBuffer.ToString();
        }
    }
}

public enum FlexSmartSdrMessageKind
{
    Version,
    Handle,
    Response,
    Status,
    Message
}

public sealed record FlexSmartSdrMessage(
    FlexSmartSdrMessageKind Kind,
    uint Sequence,
    uint HexResponse,
    string Body,
    string Handle);

public sealed record FlexSliceState(
    int Index,
    bool InUse,
    long FrequencyHz,
    string Mode,
    bool IsTransmit,
    bool IsActive,
    string FmToneMode,
    double FmToneHz,
    string PanStreamId = "");
