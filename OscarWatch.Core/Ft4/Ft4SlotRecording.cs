using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OscarWatch.Core.Ft4;

/// <summary>
/// One received FT4 slot saved for decoder testing: the raw capture (a WAV beside this file)
/// and every decode pass run on it, with the exact inputs needed to replay that pass.
/// </summary>
public sealed class Ft4RecordedSlot
{
    public DateTime SlotUtc { get; set; }
    public string? Satellite { get; set; }
    public double? ElevationDeg { get; set; }
    public int SampleRate { get; set; } = 12000;

    /// <summary>WAV file name, in the same folder as this file.</summary>
    public string Audio { get; set; } = "";

    public List<Ft4RecordedPass> Passes { get; set; } = [];

    /// <summary>
    /// Filled in by hand when a recording joins the test corpus. Null for a fresh recording.
    /// </summary>
    public Ft4SlotExpectation? Expect { get; set; }
}

/// <summary>One native decode call on a slot, and what it returned.</summary>
public sealed class Ft4RecordedPass
{
    /// <summary>Samples from the start of the capture the pass used (the early decode stops near 6 s).</summary>
    public int SampleCount { get; set; }

    /// <summary>Downlink slope taken out before decoding; 0 for the raw capture.</summary>
    public double DopplerSlopeHzPerSec { get; set; }

    public bool Deep { get; set; }
    public float FMinHz { get; set; }
    public float FMaxHz { get; set; }

    /// <summary>Newline-separated hinted messages, or null when AP was not tried.</summary>
    public string? ApHints { get; set; }

    public float ApHz { get; set; }
    public double DriftMaxHzPerSec { get; set; }
    public int DriftSteps { get; set; }
    public List<Ft4RecordedDecode> Decodes { get; set; } = [];
}

public sealed class Ft4RecordedDecode
{
    public string Text { get; set; } = "";
    public float FreqHz { get; set; }
    public float TimeSec { get; set; }
    public float SnrDb { get; set; }

    /// <summary>0 for a CRC decode, 1 for a guessed hint, 2 for a hint that passed the CRC.</summary>
    public int Ap { get; set; }

    public float DriftHzPerSec { get; set; }
}

/// <summary>What a replay of the slot must and must not produce.</summary>
public sealed class Ft4SlotExpectation
{
    /// <summary>Messages at least one pass has to decode.</summary>
    public List<string> Decode { get; set; } = [];

    /// <summary>Messages no pass may produce, such as a known false AP reply.</summary>
    public List<string> Reject { get; set; } = [];

    public string? Note { get; set; }
}

public static class Ft4SlotRecordingFiles
{
    public const int RetainedDays = 14;

    /// <summary>
    /// Lowest elevation recorded. A few degrees under the horizon gives noise-only slots
    /// around AOS and LOS for false-decode tests, without hours of empty audio between passes.
    /// </summary>
    public const double MinElevationDeg = -2;

    /// <summary>Record only while the tracked satellite is near or above the horizon; unknown elevation is skipped.</summary>
    public static bool ShouldRecord(double? elevationDeg) => elevationDeg >= MinElevationDeg;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetDefaultDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OscarWatch",
            "ft4-slots");

    /// <summary>File name without extension, for example <c>RS-44_20261007_214952.5</c>.</summary>
    public static string BuildBaseName(string? satellite, DateTime slotUtc)
    {
        var stamp = slotUtc.ToString("yyyyMMdd_HHmmss.f", CultureInfo.InvariantCulture);
        var safe = Sanitize(satellite);
        return safe.Length == 0 ? stamp : $"{safe}_{stamp}";
    }

    public static void Save(string directory, Ft4RecordedSlot slot)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.ChangeExtension(slot.Audio, ".json"));
        File.WriteAllText(path, JsonSerializer.Serialize(slot, Json));
    }

    public static Ft4RecordedSlot Load(string jsonPath) =>
        JsonSerializer.Deserialize<Ft4RecordedSlot>(File.ReadAllText(jsonPath), Json)
        ?? throw new InvalidDataException($"Empty FT4 slot recording: {jsonPath}");

    /// <summary>Delete recordings (WAV and JSON) older than <see cref="RetainedDays"/>.</summary>
    public static int PruneOlderThanRetention(string directory, DateTime? utcNow = null)
    {
        if (!Directory.Exists(directory))
            return 0;

        var cutoff = (utcNow ?? DateTime.UtcNow).AddDays(-RetainedDays);
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var ext = Path.GetExtension(path);
            if (!ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                if (File.GetLastWriteTimeUtc(path) >= cutoff)
                    continue;
                File.Delete(path);
                deleted++;
            }
            catch
            {
                // Locked or gone; the next prune tries again.
            }
        }

        return deleted;
    }

    private static string Sanitize(string? satellite)
    {
        if (string.IsNullOrWhiteSpace(satellite))
            return "";

        var sb = new StringBuilder(satellite.Length);
        foreach (var ch in satellite.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch))
                sb.Append('-');
        }

        return sb.ToString().Trim('-');
    }
}

/// <summary>Mono 16-bit PCM WAV, the format the slot recorder writes.</summary>
public static class Ft4SlotWav
{
    public static void Write(string path, ReadOnlySpan<float> samples, int sampleRate)
    {
        using var stream = File.Create(path);
        using var w = new BinaryWriter(stream);
        var dataBytes = samples.Length * 2;
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(dataBytes);
        foreach (var s in samples)
            w.Write((short)Math.Round(Math.Clamp(s, -1f, 1f) * short.MaxValue));
    }

    public static float[] Read(string path, out int sampleRate)
    {
        using var stream = File.OpenRead(path);
        using var r = new BinaryReader(stream);
        if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "RIFF")
            throw new InvalidDataException($"Not a WAV file: {path}");
        r.ReadInt32();
        if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "WAVE")
            throw new InvalidDataException($"Not a WAV file: {path}");

        sampleRate = 0;
        short channels = 0, bits = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            var id = Encoding.ASCII.GetString(r.ReadBytes(4));
            var size = r.ReadInt32();
            if (id == "fmt ")
            {
                var format = r.ReadInt16();
                channels = r.ReadInt16();
                sampleRate = r.ReadInt32();
                r.ReadInt32();
                r.ReadInt16();
                bits = r.ReadInt16();
                if (format != 1 || channels != 1 || bits != 16)
                    throw new InvalidDataException($"Expected mono 16-bit PCM: {path}");
                stream.Seek(size - 16, SeekOrigin.Current);
            }
            else if (id == "data")
            {
                if (bits != 16)
                    throw new InvalidDataException($"WAV data before its format: {path}");
                var samples = new float[size / 2];
                for (var i = 0; i < samples.Length; i++)
                    samples[i] = r.ReadInt16() / (float)short.MaxValue;
                return samples;
            }
            else
            {
                stream.Seek(size + (size & 1), SeekOrigin.Current);
            }
        }

        throw new InvalidDataException($"WAV has no data: {path}");
    }
}
