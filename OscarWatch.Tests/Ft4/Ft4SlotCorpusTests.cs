using OscarWatch.Core.Ft4;
using OscarWatch.Ft4;
using Xunit.Abstractions;

namespace OscarWatch.Tests.Ft4;

/// <summary>
/// Replays real FT4 slots saved with Settings, Modem, Save receive slot audio.
/// To add one: copy the WAV and its JSON into <c>OscarWatch.Tests/Ft4/Corpus</c>, then add an
/// <c>expect</c> block to the JSON listing messages that must decode and messages that must not
/// (for example a known false AP reply). Recordings without <c>expect</c> are replayed but not judged.
/// Set OSCARWATCH_FT4_CORPUS to replay another folder, such as the app's own ft4-slots folder.
/// </summary>
public sealed class Ft4SlotCorpusTests(ITestOutputHelper output)
{
    private const int Rate = 12000;

    [Fact]
    public void Recorded_slots_meet_their_expectations()
    {
        if (!Ft8Native.IsAvailable)
            return;

        var directory = CorpusDirectory();
        if (directory is null)
            return;

        var problems = new List<string>();
        var judged = 0;
        foreach (var json in Directory.EnumerateFiles(directory, "*.json").Order())
        {
            var slot = Ft4SlotRecordingFiles.Load(json);
            var texts = Ft4SlotReplay.Run(json, slot);
            output.WriteLine($"{Path.GetFileName(json)}: {string.Join(" | ", texts.Order())}");
            if (slot.Expect is null)
                continue;

            judged++;
            problems.AddRange(Ft4SlotReplay.Check(Path.GetFileName(json), slot.Expect, texts));
        }

        output.WriteLine($"{judged} recording(s) with expectations");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Replay_reproduces_a_recorded_pass_and_reports_misses_and_rejects()
    {
        if (!Ft8Native.IsAvailable)
            return;

        var dir = Directory.CreateTempSubdirectory("ow-ft4-corpus").FullName;
        try
        {
            var pcm = Ft8Native.EncodeFt4("CQ MM9SQL IO87", 1500f)!;
            var capture = new float[(int)(Rate * Ft4SlotClock.Ft4SlotSeconds)];
            for (var i = 0; i < pcm.Length && i < capture.Length; i++)
                capture[i] = pcm[i] * 0.5f;

            var slotUtc = new DateTime(2026, 10, 7, 21, 49, 52, 500, DateTimeKind.Utc);
            var slot = new Ft4RecordedSlot
            {
                SlotUtc = slotUtc,
                Satellite = "RS-44",
                ElevationDeg = 3,
                Audio = Ft4SlotRecordingFiles.BuildBaseName("RS-44", slotUtc) + ".wav",
                Passes =
                [
                    new Ft4RecordedPass { SampleCount = capture.Length, FMinHz = 200, FMaxHz = 2800 },
                ],
                Expect = new Ft4SlotExpectation
                {
                    Decode = ["CQ MM9SQL IO87", "CQ KK7MNC CN87"],
                    Reject = ["CQ MM9SQL IO87"],
                },
            };
            Ft4SlotWav.Write(Path.Combine(dir, slot.Audio), capture, Rate);
            Ft4SlotRecordingFiles.Save(dir, slot);

            var json = Path.Combine(dir, Path.ChangeExtension(slot.Audio, ".json"));
            Assert.Equal("RS-44_20261007_214952.5.json", Path.GetFileName(json));
            var loaded = Ft4SlotRecordingFiles.Load(json);
            var texts = Ft4SlotReplay.Run(json, loaded);
            Assert.Contains("CQ MM9SQL IO87", texts);

            var problems = Ft4SlotReplay.Check("synthetic", loaded.Expect!, texts);
            Assert.Equal(2, problems.Count);
            Assert.Contains(problems, p => p.Contains("missed CQ KK7MNC CN87", StringComparison.Ordinal));
            Assert.Contains(problems, p => p.Contains("produced rejected CQ MM9SQL IO87", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Wav_round_trip_keeps_samples_within_one_step()
    {
        var path = Path.GetTempFileName();
        try
        {
            float[] samples = [0f, 0.5f, -0.5f, 1f, -1f, 1.5f, 0.001f];
            Ft4SlotWav.Write(path, samples, Rate);
            var back = Ft4SlotWav.Read(path, out var rate);

            Assert.Equal(Rate, rate);
            Assert.Equal(samples.Length, back.Length);
            for (var i = 0; i < samples.Length; i++)
                Assert.InRange(back[i], Math.Clamp(samples[i], -1f, 1f) - 1e-4f, Math.Clamp(samples[i], -1f, 1f) + 1e-4f);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(-10.0, false)]
    [InlineData(-2.1, false)]
    [InlineData(-2.0, true)]
    [InlineData(0.0, true)]
    [InlineData(45.0, true)]
    public void Records_only_near_or_above_the_horizon(double? elevationDeg, bool expected) =>
        Assert.Equal(expected, Ft4SlotRecordingFiles.ShouldRecord(elevationDeg));

    [Fact]
    public void Prune_deletes_only_old_recordings()
    {
        var dir = Directory.CreateTempSubdirectory("ow-ft4-prune").FullName;
        try
        {
            var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
            var oldWav = Path.Combine(dir, "old.wav");
            var oldJson = Path.Combine(dir, "old.json");
            var fresh = Path.Combine(dir, "fresh.wav");
            var other = Path.Combine(dir, "notes.txt");
            foreach (var p in new[] { oldWav, oldJson, fresh, other })
                File.WriteAllText(p, "x");
            File.SetLastWriteTimeUtc(oldWav, now.AddDays(-20));
            File.SetLastWriteTimeUtc(oldJson, now.AddDays(-20));
            File.SetLastWriteTimeUtc(other, now.AddDays(-20));
            File.SetLastWriteTimeUtc(fresh, now.AddDays(-1));

            Assert.Equal(2, Ft4SlotRecordingFiles.PruneOlderThanRetention(dir, now));
            Assert.False(File.Exists(oldWav));
            Assert.False(File.Exists(oldJson));
            Assert.True(File.Exists(fresh));
            Assert.True(File.Exists(other));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string? CorpusDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("OSCARWATCH_FT4_CORPUS");
        if (!string.IsNullOrWhiteSpace(overridden))
            return Directory.Exists(overridden) ? overridden : null;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "OscarWatch.slnx")))
                continue;
            var corpus = Path.Combine(dir.FullName, "OscarWatch.Tests", "Ft4", "Corpus");
            return Directory.Exists(corpus) ? corpus : null;
        }

        return null;
    }
}

/// <summary>Runs every recorded pass of a slot through the current decoder.</summary>
internal static class Ft4SlotReplay
{
    public static HashSet<string> Run(string jsonPath, Ft4RecordedSlot slot)
    {
        var wav = Path.Combine(Path.GetDirectoryName(jsonPath)!, slot.Audio);
        var capture = Ft4SlotWav.Read(wav, out var rate);
        var texts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pass in slot.Passes)
        {
            var samples = capture.AsSpan(0, Math.Min(pass.SampleCount, capture.Length)).ToArray();
            if (pass.DopplerSlopeHzPerSec != 0)
                samples = Ft4AudioDoppler.RemoveLinearDrift(samples, rate, pass.DopplerSlopeHzPerSec);

            var decoded = Ft8Native.DecodeFt4Drift(
                samples,
                rate,
                pass.FMinHz,
                pass.FMaxHz,
                pass.Deep,
                pass.ApHints,
                pass.ApHz,
                (float)pass.DriftMaxHzPerSec,
                pass.DriftSteps);
            foreach (var d in decoded)
                texts.Add(d.text);
        }

        return texts;
    }

    public static List<string> Check(string name, Ft4SlotExpectation expect, IReadOnlySet<string> texts)
    {
        var problems = new List<string>();
        foreach (var must in expect.Decode.Where(m => !texts.Contains(m)))
            problems.Add($"{name}: missed {must}");
        foreach (var bad in expect.Reject.Where(texts.Contains))
            problems.Add($"{name}: produced rejected {bad}");
        return problems;
    }
}
