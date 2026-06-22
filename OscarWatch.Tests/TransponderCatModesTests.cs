using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class TransponderCatModesTests
{
    [Theory]
    [InlineData("FM-DATA", "DATA-FM")]
    [InlineData("data-fm", "DATA-FM")]
    [InlineData(" USB ", "USB")]
    public void Normalize_canonicalizes_mode_strings(string input, string expected) =>
        Assert.Equal(expected, TransponderCatModes.Normalize(input));

    [Fact]
    public void EditorOptions_includes_data_fm() =>
        Assert.Contains("DATA-FM", TransponderCatModes.EditorOptions);
}
