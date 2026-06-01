namespace OscarWatch.Recording;

/// <summary>Bounded byte ring for PCM16 capture; producer is the PortAudio callback, consumer is the writer thread.</summary>
internal sealed class PcmRingBuffer
{
    private readonly byte[] _buffer;
    private int _read;
    private int _write;
    private int _count;
    private long _droppedBytes;
    private int _peakCount;

    public PcmRingBuffer(int capacityBytes)
    {
        if (capacityBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(capacityBytes));

        _buffer = new byte[capacityBytes];
    }

    public int Capacity => _buffer.Length;

    public int Count
    {
        get { lock (this) return _count; }
    }

    public long DroppedBytes => Interlocked.Read(ref _droppedBytes);

    public int PeakCount => Volatile.Read(ref _peakCount);

    public void RecordDropped(int byteCount)
    {
        if (byteCount > 0)
            Interlocked.Add(ref _droppedBytes, byteCount);
    }

    public void ResetStats()
    {
        Interlocked.Exchange(ref _droppedBytes, 0);
        Volatile.Write(ref _peakCount, 0);
    }

    /// <summary>Copies PCM bytes into the ring; drops the whole chunk if it does not fit.</summary>
    public void TryWrite(ReadOnlySpan<byte> pcm)
    {
        if (pcm.IsEmpty)
            return;

        lock (this)
        {
            if (pcm.Length > _buffer.Length - _count)
            {
                RecordDropped(pcm.Length);
                return;
            }

            CopyIn(pcm);
            UpdatePeakCount();
        }
    }

    /// <summary>Drains up to <paramref name="destination"/> bytes; returns bytes copied.</summary>
    public int Read(Span<byte> destination)
    {
        if (destination.IsEmpty)
            return 0;

        lock (this)
        {
            var toRead = Math.Min(destination.Length, _count);
            if (toRead == 0)
                return 0;

            CopyOut(destination[..toRead]);
            return toRead;
        }
    }

    private void UpdatePeakCount()
    {
        var peak = _peakCount;
        while (_count > peak)
        {
            var observed = Interlocked.CompareExchange(ref _peakCount, _count, peak);
            if (observed == peak)
                break;
            peak = observed;
        }
    }

    private void CopyIn(ReadOnlySpan<byte> source)
    {
        var remaining = source.Length;
        var offset = 0;
        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, _buffer.Length - _write);
            source.Slice(offset, chunk).CopyTo(_buffer.AsSpan(_write));
            _write = (_write + chunk) % _buffer.Length;
            remaining -= chunk;
            offset += chunk;
        }

        _count += source.Length;
    }

    private void CopyOut(Span<byte> destination)
    {
        var remaining = destination.Length;
        var offset = 0;
        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, _buffer.Length - _read);
            _buffer.AsSpan(_read, chunk).CopyTo(destination[offset..]);
            _read = (_read + chunk) % _buffer.Length;
            remaining -= chunk;
            offset += chunk;
        }

        _count -= destination.Length;
    }
}
