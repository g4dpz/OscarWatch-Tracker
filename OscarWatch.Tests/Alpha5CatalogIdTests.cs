using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

public sealed class Alpha5CatalogIdTests
{
    [Theory]
    [InlineData(0, "00000")]
    [InlineData(7530, "07530")]
    [InlineData(99999, "99999")]
    [InlineData(100000, "A0000")]
    [InlineData(148493, "E8493")]
    [InlineData(339999, "Z9999")]
    public void TryEncode_matches_space_track_examples(int noradCatId, string expected)
    {
        Assert.True(Alpha5CatalogId.TryEncode(noradCatId, out var field5));
        Assert.Equal(expected, field5);
    }

    [Theory]
    [InlineData("00000", 0)]
    [InlineData("07530", 7530)]
    [InlineData("7530", 7530)]
    [InlineData("A0000", 100000)]
    [InlineData("a0000", 100000)]
    [InlineData("E8493", 148493)]
    [InlineData("100000", 100000)]
    [InlineData("Z9999", 339999)]
    public void TryDecode_round_trips_numeric_and_alpha5(string field, int expected)
    {
        Assert.True(Alpha5CatalogId.TryDecode(field, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(100000)]
    [InlineData(148493)]
    [InlineData(339999)]
    [InlineData(7530)]
    public void Encode_decode_round_trip(int noradCatId)
    {
        Assert.True(Alpha5CatalogId.TryEncode(noradCatId, out var field5));
        Assert.True(Alpha5CatalogId.TryDecode(field5, out var decoded));
        Assert.Equal(noradCatId, decoded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(340000)]
    public void TryEncode_rejects_out_of_range(int noradCatId)
    {
        Assert.False(Alpha5CatalogId.TryEncode(noradCatId, out _));
    }

    [Theory]
    [InlineData("I0000")]
    [InlineData("O0000")]
    [InlineData("abc")]
    [InlineData("340000")]
    [InlineData("")]
    public void TryDecode_rejects_invalid(string field)
    {
        Assert.False(Alpha5CatalogId.TryDecode(field, out _));
    }

    [Fact]
    public void IsAlpha5_detects_letter_prefix_only()
    {
        Assert.True(Alpha5CatalogId.IsAlpha5("A0000"));
        Assert.False(Alpha5CatalogId.IsAlpha5("07530"));
        Assert.False(Alpha5CatalogId.IsAlpha5("I0000"));
    }

    [Fact]
    public void Normalize_canonicalises_to_five_character_field()
    {
        Assert.Equal("A0000", Alpha5CatalogId.Normalize("100000"));
        Assert.Equal("A0000", Alpha5CatalogId.Normalize("a0000"));
        Assert.Equal("07530", Alpha5CatalogId.Normalize("7530"));
        Assert.Null(Alpha5CatalogId.Normalize("I0000"));
    }
}
