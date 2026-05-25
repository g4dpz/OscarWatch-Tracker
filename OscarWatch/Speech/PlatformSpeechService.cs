using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OscarWatch.Core.Services;

namespace OscarWatch.Speech;

public sealed class PlatformSpeechService : ISpeechService
{
    private static readonly SpeechVoiceOption SystemDefault = new("", "System default");
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private readonly Lazy<LinuxBackend?> _linuxBackend = new(DetectLinuxBackend);

    public bool IsAvailable
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return File.Exists("/usr/bin/say");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return _linuxBackend.Value is not null;

            return false;
        }
    }

    public IReadOnlyList<SpeechVoiceOption> GetAvailableVoices()
    {
        if (!IsAvailable)
            return [SystemDefault];

        var voices = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetWindowsVoices()
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? GetMacVoices()
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? GetLinuxVoices()
            : [];

        return voices.Count == 0 ? [SystemDefault] : PrependDefault(voices);
    }

    public async Task SpeakAsync(string text, string? voiceName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || !IsAvailable)
            return;

        await _speakLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                await SpeakWindowsAsync(text, voiceName, cancellationToken).ConfigureAwait(false);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                await RunSpeechProcessAsync("/usr/bin/say", BuildMacArgs(voiceName, text), cancellationToken)
                    .ConfigureAwait(false);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                await SpeakLinuxAsync(text, voiceName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _speakLock.Release();
        }
    }

    private static IReadOnlyList<SpeechVoiceOption> PrependDefault(IReadOnlyList<SpeechVoiceOption> voices)
    {
        if (voices.Count > 0 && string.IsNullOrEmpty(voices[0].Id))
            return voices;

        var list = new List<SpeechVoiceOption>(voices.Count + 1) { SystemDefault };
        list.AddRange(voices.Where(v => !string.IsNullOrEmpty(v.Id)));
        return list;
    }

    private static IReadOnlyList<SpeechVoiceOption> GetWindowsVoices()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        try
        {
            return WindowsSpeechHelper.GetVoices();
        }
        catch
        {
            return [];
        }
    }

    private static Task SpeakWindowsAsync(string text, string? voiceName, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return Task.CompletedTask;

        return WindowsSpeechHelper.SpeakAsync(text, voiceName, cancellationToken);
    }

    private static IReadOnlyList<SpeechVoiceOption> GetMacVoices()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/say",
                Arguments = "-v ?",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return [];

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseMacVoiceLine)
                .Where(v => v is not null)
                .Cast<SpeechVoiceOption>()
                .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static SpeechVoiceOption? ParseMacVoiceLine(string line)
    {
        var hashIndex = line.IndexOf('#');
        var left = hashIndex >= 0 ? line[..hashIndex].Trim() : line.Trim();
        if (left.Length == 0)
            return null;

        var parts = left.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        var name = parts[0];
        var locale = parts.Length > 1 ? parts[1] : "";
        var display = string.IsNullOrEmpty(locale) ? name : $"{name} ({locale})";
        return new SpeechVoiceOption(name, display);
    }

    private static IReadOnlyList<string> BuildMacArgs(string? voiceName, string text)
    {
        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            args.Add("-v");
            args.Add(voiceName);
        }

        args.Add(text);
        return args;
    }

    private static IReadOnlyList<SpeechVoiceOption> GetLinuxVoices()
    {
        var backend = DetectLinuxBackend();
        if (backend == LinuxBackend.EspeakNg)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "espeak-ng",
                    Arguments = "--voices",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is null)
                    return [];

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Skip(1)
                    .Select(ParseEspeakVoiceLine)
                    .Where(v => v is not null)
                    .Cast<SpeechVoiceOption>()
                    .DistinctBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        return [];
    }

    private static SpeechVoiceOption? ParseEspeakVoiceLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
            return null;

        var language = parts[1];
        var name = parts[3];
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new SpeechVoiceOption(name, $"{name} ({language})");
    }

    private async Task SpeakLinuxAsync(string text, string? voiceName, CancellationToken cancellationToken)
    {
        switch (_linuxBackend.Value)
        {
            case LinuxBackend.EspeakNg:
            {
                var args = new List<string> { "-s", "150" };
                if (!string.IsNullOrWhiteSpace(voiceName))
                {
                    args.Add("-v");
                    args.Add(voiceName);
                }

                args.Add(text);
                await RunSpeechProcessAsync("espeak-ng", args, cancellationToken).ConfigureAwait(false);
                break;
            }
            case LinuxBackend.SpeechDispatcher:
                await RunSpeechProcessAsync("spd-say", [text], cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static async Task RunSpeechProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LinuxBackend? DetectLinuxBackend()
    {
        if (CommandExists("espeak-ng"))
            return LinuxBackend.EspeakNg;

        if (CommandExists("spd-say"))
            return LinuxBackend.SpeechDispatcher;

        return null;
    }

    private static bool CommandExists(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private enum LinuxBackend
    {
        EspeakNg,
        SpeechDispatcher
    }
}
