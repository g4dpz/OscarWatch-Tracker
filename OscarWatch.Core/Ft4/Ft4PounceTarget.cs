using System.Text.RegularExpressions;

namespace OscarWatch.Core.Ft4;

public enum Ft4PounceTargetKind
{
    Callsign,
    Grid
}

/// <summary>
/// The station (or grid) the operator is waiting for. Pounce fires when the target
/// calls CQ or calls us, never while they are working someone else.
/// </summary>
public sealed partial class Ft4PounceTarget
{
    private Ft4PounceTarget(Ft4PounceTargetKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    public Ft4PounceTargetKind Kind { get; }

    /// <summary>Upper-case callsign, or upper-case Maidenhead grid as entered (4 or 6 characters).</summary>
    public string Value { get; }

    public static bool TryParse(string? text, out Ft4PounceTarget target)
    {
        target = null!;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (GridPattern().IsMatch(trimmed))
        {
            target = new Ft4PounceTarget(Ft4PounceTargetKind.Grid, trimmed.ToUpperInvariant());
            return true;
        }

        var call = Ft4MessageCodec.NormalizeCall(trimmed);
        if (!CallPattern().IsMatch(call) || BaseCall(call).Length == 0)
            return false;

        target = new Ft4PounceTarget(Ft4PounceTargetKind.Callsign, call);
        return true;
    }

    /// <summary>
    /// True when the line comes from the target, whatever they are sending.
    /// Used to mark rows in the decode list.
    /// </summary>
    public bool IsFrom(Ft4DecodedMessage message)
    {
        if (!message.IsReceiveActivity || message.IsRejected)
            return false;

        if (Kind == Ft4PounceTargetKind.Callsign)
        {
            var de = BaseCall(Ft4MessageCodec.NormalizeCall(message.CallDe ?? ""));
            return de.Length > 0 && de.Equals(BaseCall(Value), StringComparison.OrdinalIgnoreCase);
        }

        // Standard FT4 messages only carry the 4-character field.
        var field = Ft4DecodeHighlight.GridField(message.Extra);
        return field is not null && field.Equals(Value[..4], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when this decode should trigger the automatic call.</summary>
    public bool Matches(Ft4DecodedMessage message, string? myCall)
    {
        if (!IsFrom(message))
            return false;

        if (Ft4MessageCodec.IsCq(message.CallTo))
            return true;

        var mine = Ft4MessageCodec.NormalizeCall(myCall ?? "");
        return mine.Length > 0 && Ft4MessageCodec.IsAddressedTo(message.CallTo, mine);
    }

    /// <summary>
    /// The home callsign without portable prefixes or suffixes:
    /// the longest '/' part that has both a letter and a digit.
    /// </summary>
    public static string BaseCall(string call)
    {
        var best = "";
        foreach (var part in call.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length > best.Length && part.Any(char.IsLetter) && part.Any(char.IsDigit))
                best = part;
        }

        return best;
    }

    [GeneratedRegex(@"^[A-R]{2}[0-9]{2}([A-X]{2})?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GridPattern();

    [GeneratedRegex(@"^[A-Z0-9/]{3,}$", RegexOptions.CultureInvariant)]
    private static partial Regex CallPattern();
}
