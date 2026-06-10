using OscarWatch.Core.Cloudlog;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 11.1**
///
/// Edge-case tests verifying that <see cref="CloudlogUrlHelper.NormalizeBaseUrl"/>
/// returns an empty string for null or whitespace input.
/// </summary>
public sealed class CloudlogUrlHelperTests
{
    /// <summary>
    /// Null or whitespace input returns an empty string.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_whitespace_input_returns_empty_string(string? input)
    {
        var result = CloudlogUrlHelper.NormalizeBaseUrl(input);

        Assert.Equal(string.Empty, result);
    }
}
