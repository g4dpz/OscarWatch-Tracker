using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

public sealed class MaidenheadLocatorTests
{
    [Theory]
    [InlineData("2m0sql", "2M0SQL")]
    [InlineData(" g0abc ", "G0ABC")]
    [InlineData("", "")]
    public void NormalizeCallsign_uppercases_and_trims(string input, string expected) =>
        Assert.Equal(expected, MaidenheadLocator.NormalizeCallsign(input));

    [Theory]
    [InlineData("io77, io87", "IO77, IO87")]
    [InlineData("io87ip", "IO87IP")]
    public void UppercaseGridEntry_uppercases_without_reformatting(string input, string expected) =>
        Assert.Equal(expected, MaidenheadLocator.UppercaseGridEntry(input));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("IO77", true)]
    [InlineData("IO7", false)]
    [InlineData("IO77,IO87", true)]
    public void GetLiveValidationState_reflects_current_entry(string? input, bool? expected) =>
        Assert.Equal(expected, MaidenheadLocator.GetLiveValidationState(input));

    [Theory]
    [InlineData("io87ip", "IO87IP")]
    [InlineData("io77, io87", "IO77,IO87")]
    [InlineData("IO77/IO87", "IO77,IO87")]
    [InlineData("io77; io87", "IO77,IO87")]
    [InlineData("IO77 IO87", "IO77,IO87")]
    [InlineData("io77,io77", "IO77")]
    public void NormalizeGrids_uppercases_and_joins(string input, string expected) =>
        Assert.Equal(expected, MaidenheadLocator.NormalizeGrids(input));

    [Theory]
    [InlineData("IO")]
    [InlineData("IO77")]
    [InlineData("IO87IP")]
    [InlineData("IO87IP62")]
    [InlineData("IO77,IO87")]
    [InlineData("IO77,IO87,IO76,IO86")]
    public void TryValidateGrids_accepts_valid_locators(string input)
    {
        Assert.True(MaidenheadLocator.TryValidateGrids(input, out var normalized, out var error, out _));
        Assert.Equal(GridValidationError.None, error);
        Assert.Equal(MaidenheadLocator.NormalizeGrids(input), normalized);
    }

    [Theory]
    [InlineData("IO771")]
    [InlineData("IO87I")]
    [InlineData("ZZ77")]
    public void TryValidateGrids_rejects_invalid_segments(string input)
    {
        Assert.False(MaidenheadLocator.TryValidateGrids(input, out _, out var error, out _));
        Assert.Equal(GridValidationError.InvalidSegment, error);
    }

    [Fact]
    public void TryValidateGrids_rejects_more_than_four_grids()
    {
        Assert.False(MaidenheadLocator.TryValidateGrids("IO77,IO87,IO76,IO86,IO75", out _, out var error, out _));
        Assert.Equal(GridValidationError.TooManyGrids, error);
    }
}
