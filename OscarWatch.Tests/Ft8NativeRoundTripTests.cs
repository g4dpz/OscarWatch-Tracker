using OscarWatch.Ft4;

namespace OscarWatch.Tests.Ft4;

public sealed class Ft8NativeRoundTripTests
{
    [Fact]
    public void Encode_then_decode_standard_cq()
    {
        if (!Ft8Native.IsAvailable)
        {
            // Native library not on PATH/output yet (e.g. clean CI without RID copy).
            return;
        }

        const string message = "CQ MM9SQL IO85";
        var pcm = Ft8Native.EncodeFt4(message, freqHz: 1200f);
        Assert.NotNull(pcm);
        Assert.True(pcm!.Length > 12000 * 4);

        var decoded = Ft8Native.DecodeFt4(pcm);
        Assert.NotEmpty(decoded);
        Assert.Contains(decoded, d => d.text.Contains("MM9SQL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decoded, d => d.text.Contains("CQ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Encode_report_message()
    {
        if (!Ft8Native.IsAvailable)
            return;

        var pcm = Ft8Native.EncodeFt4("G4ABC MM9SQL +05", freqHz: 1500f);
        Assert.NotNull(pcm);
        var decoded = Ft8Native.DecodeFt4(pcm!);
        Assert.Contains(decoded, d =>
            d.text.Contains("G4ABC", StringComparison.OrdinalIgnoreCase)
            && d.text.Contains("MM9SQL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Encode_portable_callsign_MM9SQL_M()
    {
        if (!Ft8Native.IsAvailable)
            return;

        // Hashed portable calls are valid FT4; pack77 needs remember_callsign first.
        Assert.True(
            Ft8Native.TryEncodeFt4("CQ MM9SQL/M IO85", 1200f, 12000, out var pcm, out var error),
            error);
        Assert.NotNull(pcm);
        Assert.True(pcm!.Length > 12000 * 4);
    }
}
