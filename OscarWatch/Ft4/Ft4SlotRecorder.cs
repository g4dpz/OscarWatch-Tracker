using OscarWatch.Core.Ft4;
using Serilog;

namespace OscarWatch.Ft4;

/// <summary>
/// Saves receive slots for decoder testing: the full capture as a WAV, and every decode
/// pass with its inputs and output as JSON. The JSON is written a slot later, once the
/// end-of-slot passes have finished.
/// </summary>
internal sealed class Ft4SlotRecorder
{
    private static readonly ILogger Log = Serilog.Log.ForContext<Ft4SlotRecorder>();

    private readonly Func<bool> _enabled;
    private readonly string _directory;
    private readonly object _gate = new();
    private readonly Dictionary<DateTime, Ft4RecordedSlot> _open = new();
    private bool _pruned;

    public Ft4SlotRecorder(Func<bool> enabled, string? directory = null)
    {
        _enabled = enabled;
        _directory = directory ?? Ft4SlotRecordingFiles.GetDefaultDirectory();
    }

    public bool Enabled => _enabled();

    public string Directory => _directory;

    public void NotePass(DateTime slotUtc, Ft4RecordedPass pass)
    {
        if (!Enabled)
            return;

        lock (_gate)
            GetOrAdd(slotUtc).Passes.Add(pass);
    }

    /// <summary>Write the slot's full capture. Call once the slot has ended.</summary>
    public void SaveAudio(DateTime slotUtc, float[] samples, int sampleRate, string? satellite, double? elevationDeg)
    {
        if (!Enabled)
            return;

        Ft4RecordedSlot slot;
        lock (_gate)
        {
            slot = GetOrAdd(slotUtc);
            slot.Satellite = string.IsNullOrWhiteSpace(satellite) ? null : satellite;
            slot.ElevationDeg = elevationDeg;
            slot.SampleRate = sampleRate;
            slot.Audio = Ft4SlotRecordingFiles.BuildBaseName(satellite, slotUtc) + ".wav";
        }

        var path = Path.Combine(_directory, slot.Audio);
        _ = Task.Run(() =>
        {
            try
            {
                System.IO.Directory.CreateDirectory(_directory);
                if (!_pruned)
                {
                    _pruned = true;
                    Ft4SlotRecordingFiles.PruneOlderThanRetention(_directory);
                }

                Ft4SlotWav.Write(path, samples, sampleRate);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FT4 slot audio could not be saved to {Path}", path);
            }
        });
    }

    /// <summary>Write the JSON for every slot that started before <paramref name="beforeUtc"/>.</summary>
    public void Flush(DateTime beforeUtc)
    {
        List<Ft4RecordedSlot> done;
        lock (_gate)
        {
            done = _open.Values.Where(s => s.SlotUtc < beforeUtc).ToList();
            foreach (var s in done)
                _open.Remove(s.SlotUtc);
        }

        foreach (var slot in done)
        {
            // No audio means the slot was a transmit slot or the capture was too short.
            if (slot.Audio.Length == 0)
                continue;

            try
            {
                Ft4SlotRecordingFiles.Save(_directory, slot);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FT4 slot recording could not be saved for {Slot}", slot.SlotUtc);
            }
        }
    }

    public void FlushAll() => Flush(DateTime.MaxValue);

    private Ft4RecordedSlot GetOrAdd(DateTime slotUtc)
    {
        if (!_open.TryGetValue(slotUtc, out var slot))
        {
            slot = new Ft4RecordedSlot { SlotUtc = slotUtc };
            _open[slotUtc] = slot;
        }

        return slot;
    }
}
