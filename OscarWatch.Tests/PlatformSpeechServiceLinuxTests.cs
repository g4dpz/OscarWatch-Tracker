using OscarWatch.Speech;

namespace OscarWatch.Tests;

public sealed class EspeakVoiceParserTests
{
    [Theory]
    [InlineData(" 5  en-gb          M  english              gmw/en", "english", "english (en-gb)")]
    [InlineData(" 2  af             M  Afrikaans            gmw/af", "Afrikaans", "Afrikaans (af)")]
    public void ParseVoiceLine_parses_espeak_ng_output(string line, string expectedId, string expectedDisplay)
    {
        var voice = EspeakVoiceParser.ParseVoiceLine(line);

        Assert.NotNull(voice);
        Assert.Equal(expectedId, voice!.Id);
        Assert.Equal(expectedDisplay, voice.DisplayName);
    }

    [Theory]
    [InlineData("Pty Language Age/Gender VoiceName File")]
    [InlineData("")]
    [InlineData("too few")]
    public void ParseVoiceLine_rejects_header_and_short_lines(string line)
    {
        Assert.Null(EspeakVoiceParser.ParseVoiceLine(line));
    }
}

public sealed class PlatformSpeechServiceLinuxTests
{
    [Theory]
    [InlineData("Annie  en-us", "Annie", "Annie en-us")]
    [InlineData("Voice Language", null, null)]
    public void ParseSpeechDispatcherVoiceLine_parses_spd_say_output(string line, string? expectedId, string? expectedDisplay)
    {
        var voice = PlatformSpeechService.ParseSpeechDispatcherVoiceLine(line);

        if (expectedId is null)
        {
            Assert.Null(voice);
            return;
        }

        Assert.NotNull(voice);
        Assert.Equal(expectedId, voice!.Id);
        Assert.Equal(expectedDisplay, voice.DisplayName);
    }
}
