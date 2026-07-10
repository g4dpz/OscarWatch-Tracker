using OscarWatch.Core.Services;

namespace OscarWatch.Speech;

internal static class EspeakVoiceParser
{
    /// <summary>Parse a line from <c>espeak-ng --voices</c> / <c>espeak --voices</c>.</summary>
    public static SpeechVoiceOption? ParseVoiceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)
            || line.StartsWith("Pty", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
            return null;

        var language = parts[1];
        var name = parts[3];
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new SpeechVoiceOption(name, $"{name} ({language})");
    }
}
