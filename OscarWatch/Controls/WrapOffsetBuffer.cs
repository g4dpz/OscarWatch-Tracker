using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OscarWatch.Controls;

/// <summary>
/// Stack-allocated buffer holding up to 3 wrap offsets (0, +w, -w).
/// Eliminates iterator state-machine allocations on the render path.
/// </summary>
internal ref struct WrapOffsetBuffer
{
    private double _v0;
    private double _v1;
    private double _v2;
    private int _count;

    public void Add(double value)
    {
        switch (_count)
        {
            case 0: _v0 = value; break;
            case 1: _v1 = value; break;
            case 2: _v2 = value; break;
            default:
                throw new InvalidOperationException("WrapOffsetBuffer capacity (3) exceeded.");
        }
        _count++;
    }

    public readonly int Count => _count;

    public readonly ReadOnlySpan<double> AsSpan()
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.AsRef(in _v0), _count);
    }
}
