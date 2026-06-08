using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

/// <summary>
/// Pre-allocated buffer that partitions satellite indices into non-focused and focused groups
/// without LINQ, iterators, or intermediate collections. Eliminates all per-frame allocation
/// in label ordering.
/// </summary>
internal struct LabelOrderBuffer
{
    private readonly int[] _indices;
    private int _count;

    public LabelOrderBuffer(int capacity)
    {
        _indices = new int[capacity];
        _count = 0;
    }

    /// <summary>
    /// Builds the ordered index sequence: non-focused visible satellites first,
    /// then the focused satellite last (so it renders on top).
    /// </summary>
    public void Build(IReadOnlyList<SatelliteTrackState> states, string? focusedNoradId, bool soloFocused)
    {
        _count = 0;
        var focusedIdx = -1;

        for (var i = 0; i < states.Count; i++)
        {
            if (!TrackingPlotAccessibility.IsPlotSatelliteVisible(soloFocused, focusedNoradId, states[i].NoradId))
                continue;

            if (string.Equals(states[i].NoradId, focusedNoradId, StringComparison.Ordinal))
            {
                focusedIdx = i;
            }
            else
            {
                EnsureCapacity();
                _indices[_count++] = i;
            }
        }

        // Append focused index last so it renders on top.
        if (focusedIdx >= 0)
        {
            EnsureCapacity();
            _indices[_count++] = focusedIdx;
        }
    }

    /// <summary>
    /// Returns the ordered indices as a span. Non-focused visible satellites appear first,
    /// followed by the focused satellite (if any) at the end.
    /// </summary>
    public ReadOnlySpan<int> Indices => _indices.AsSpan(0, _count);

    private void EnsureCapacity()
    {
        // The buffer is pre-allocated with capacity 64 which exceeds typical satellite counts.
        // If we ever hit this, it means the caller should increase the initial capacity.
        // In practice satellite count is ≤ 30, so this is a safety net only.
        if (_count >= _indices.Length)
            throw new InvalidOperationException(
                $"LabelOrderBuffer capacity ({_indices.Length}) exceeded. Increase initial capacity.");
    }
}
