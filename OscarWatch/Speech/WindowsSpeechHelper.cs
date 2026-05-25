using System.Runtime.Versioning;
using System.Speech.Synthesis;
using OscarWatch.Core.Services;

namespace OscarWatch.Speech;

[SupportedOSPlatform("windows")]
internal static class WindowsSpeechHelper
{
    public static IReadOnlyList<SpeechVoiceOption> GetVoices()
    {
        using var synth = new SpeechSynthesizer();
        return synth.GetInstalledVoices()
            .Where(v => v.Enabled)
            .Select(v => new SpeechVoiceOption(
                v.VoiceInfo.Name,
                string.IsNullOrWhiteSpace(v.VoiceInfo.Description)
                    ? v.VoiceInfo.Name
                    : v.VoiceInfo.Description))
            .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void Speak(string text, string? voiceName)
    {
        using var synth = new SpeechSynthesizer();
        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            try
            {
                synth.SelectVoice(voiceName);
            }
            catch
            {
                // fall back to default voice
            }
        }

        synth.Speak(text);
    }

    public static Task SpeakAsync(string text, string? voiceName, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                Speak(text, voiceName);
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
