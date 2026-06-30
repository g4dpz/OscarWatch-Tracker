// Feature: render-path-allocation-reduction, Property 1: Cache lookup idempotency

using Avalonia.Media;
using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.1, 1.2, 1.3**
///
/// Property-based tests verifying that <see cref="RenderResourceCache"/> returns the same
/// object reference for same-key lookups (cache idempotency), and that Clear() invalidates
/// cached instances so new ones are produced on subsequent calls.
/// </summary>
public class RenderPathAllocationReductionPropertyTests
{
    /// <summary>
    /// Property 1: Cache lookup idempotency — GetPen.
    ///
    /// For any colour and thickness, calling GetPen twice with the same arguments
    /// SHALL return the same object reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GetPen_returns_same_reference_for_same_inputs(byte r, byte g, byte b, byte a, int rawThickness)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = Math.Abs(rawThickness % 10) + 0.5; // 0.5 to 10.5
        var cache = new RenderResourceCache();
        var pen1 = cache.GetPen(color, thickness);
        var pen2 = cache.GetPen(color, thickness);
        return ReferenceEquals(pen1, pen2);
    }

    /// <summary>
    /// Property 1: Cache lookup idempotency — GetBrush.
    ///
    /// For any colour, calling GetBrush twice with the same arguments
    /// SHALL return the same object reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GetBrush_returns_same_reference_for_same_inputs(byte r, byte g, byte b, byte a)
    {
        var color = Color.FromArgb(a, r, g, b);
        var cache = new RenderResourceCache();
        var brush1 = cache.GetBrush(color);
        var brush2 = cache.GetBrush(color);
        return ReferenceEquals(brush1, brush2);
    }

    /// <summary>
    /// After Clear(), the cache SHALL return new instances (not the previously cached ones).
    /// This validates that theme-change invalidation works correctly.
    /// </summary>
    [Fact]
    public void After_Clear_new_instances_returned()
    {
        var color = Color.FromArgb(255, 100, 150, 200);
        const double thickness = 2.0;
        var cache = new RenderResourceCache();

        var penBefore = cache.GetPen(color, thickness);
        var brushBefore = cache.GetBrush(color);

        cache.Clear();

        var penAfter = cache.GetPen(color, thickness);
        var brushAfter = cache.GetBrush(color);

        Assert.False(ReferenceEquals(penBefore, penAfter),
            "GetPen should return a new instance after Clear()");
        Assert.False(ReferenceEquals(brushBefore, brushAfter),
            "GetBrush should return a new instance after Clear()");
    }
}
