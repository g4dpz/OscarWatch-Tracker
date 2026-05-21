namespace OscarWatch.Core.Services;

public sealed record SpeechVoiceOption(string Id, string DisplayName);

public interface ISpeechService
{
    bool IsAvailable { get; }

    IReadOnlyList<SpeechVoiceOption> GetAvailableVoices();

    Task SpeakAsync(string text, string? voiceName = null, CancellationToken cancellationToken = default);
}
