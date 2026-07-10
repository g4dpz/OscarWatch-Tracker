using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.Speech;

public sealed class PlatformSpeechService : ISpeechService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<PlatformSpeechService>();
    private static readonly SpeechVoiceOption SystemDefault = new("", "System default");
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private readonly Lazy<LinuxSpeechBackend?> _linuxBackend = new(DetectLinuxBackend);

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
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? GetLinuxVoices(_linuxBackend.Value)
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
                await SpeakLinuxAsync(_linuxBackend.Value, text, voiceName, cancellationToken).ConfigureAwait(false);
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

    private static IReadOnlyList<SpeechVoiceOption> GetLinuxVoices(LinuxSpeechBackend? backend)
    {
        if (backend is null)
            return [];

        return backend.Kind switch
        {
            LinuxBackendKind.Espeak => GetEspeakVoices(backend.Executable),
            LinuxBackendKind.SpeechDispatcher => GetSpeechDispatcherVoices(),
            _ => []
        };
    }

    private static IReadOnlyList<SpeechVoiceOption> GetEspeakVoices(string executable)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
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
            if (process.ExitCode != 0)
                return [];

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(EspeakVoiceParser.ParseVoiceLine)
                .Where(v => v is not null)
                .Cast<SpeechVoiceOption>()
                .DistinctBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to list {Executable} voices", executable);
            return [];
        }
    }

    private static IReadOnlyList<SpeechVoiceOption> GetSpeechDispatcherVoices()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "spd-say",
                Arguments = "-L",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return [];

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                return [];

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseSpeechDispatcherVoiceLine)
                .Where(v => v is not null)
                .Cast<SpeechVoiceOption>()
                .DistinctBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to list speech-dispatcher voices");
            return [];
        }
    }

    internal static SpeechVoiceOption? ParseSpeechDispatcherVoiceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)
            || line.StartsWith("Voice", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("NAME", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        var name = parts[0];
        if (string.Equals(name, "Voice", StringComparison.OrdinalIgnoreCase))
            return null;

        var display = parts.Length > 1 ? string.Join(' ', parts) : name;
        return new SpeechVoiceOption(name, display);
    }

    private static async Task SpeakLinuxAsync(
        LinuxSpeechBackend? backend,
        string text,
        string? voiceName,
        CancellationToken cancellationToken)
    {
        if (backend is null)
            return;

        switch (backend.Kind)
        {
            case LinuxBackendKind.Espeak:
            {
                var args = new List<string> { "-s", "150" };
                if (!string.IsNullOrWhiteSpace(voiceName))
                {
                    args.Add("-v");
                    args.Add(voiceName);
                }

                args.Add(text);
                await RunSpeechProcessAsync(backend.Executable, args, cancellationToken).ConfigureAwait(false);
                break;
            }
            case LinuxBackendKind.SpeechDispatcher:
            {
                var args = new List<string>();
                if (!string.IsNullOrWhiteSpace(voiceName))
                {
                    args.Add("-y");
                    args.Add(voiceName);
                }

                args.Add(text);
                await RunSpeechProcessAsync(backend.Executable, args, cancellationToken).ConfigureAwait(false);
                break;
            }
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
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}.");
    }

    private static LinuxSpeechBackend? DetectLinuxBackend()
    {
        if (CommandExists("espeak-ng"))
            return new LinuxSpeechBackend("espeak-ng", LinuxBackendKind.Espeak);

        if (CommandExists("espeak"))
            return new LinuxSpeechBackend("espeak", LinuxBackendKind.Espeak);

        if (CommandExists("spd-say"))
            return new LinuxSpeechBackend("spd-say", LinuxBackendKind.SpeechDispatcher);

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

    private sealed record LinuxSpeechBackend(string Executable, LinuxBackendKind Kind);

    private enum LinuxBackendKind
    {
        Espeak,
        SpeechDispatcher
    }
}
