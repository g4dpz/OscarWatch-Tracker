// Feature: test-coverage-expansion, Property 27: URL Protocol Prepend
// Feature: test-coverage-expansion, Property 28: URL No Trailing Slash
// Feature: test-coverage-expansion, Property 29: URL Whitespace Idempotence
// Feature: test-coverage-expansion, Property 30: URL No Double Protocol

using FsCheck.Xunit;
using OscarWatch.Core.Cloudlog;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 11.2, 11.3, 11.4, 11.5, 11.6**
///
/// Property-based tests verifying that <see cref="CloudlogUrlHelper.NormalizeBaseUrl"/>
/// correctly prepends protocol, removes trailing slashes, trims whitespace,
/// and avoids double-protocol prepending.
/// </summary>
public class CloudlogUrlHelperPropertyTests
{
    /// <summary>
    /// Property 27: URL Protocol Prepend.
    ///
    /// For any non-whitespace string that does not contain "://",
    /// NormalizeBaseUrl shall return a result that starts with "https://".
    /// </summary>
    [Property]
    public bool Input_without_protocol_gets_https_prepended(string input)
    {
        // Skip null and whitespace-only strings (those return empty string)
        if (string.IsNullOrWhiteSpace(input))
            return true;

        // Skip inputs that already contain "://"
        if (input.Contains("://"))
            return true;

        var result = CloudlogUrlHelper.NormalizeBaseUrl(input);

        return result.StartsWith("https://", StringComparison.Ordinal);
    }

    /// <summary>
    /// Property 28: URL No Trailing Slash.
    ///
    /// For any non-null, non-whitespace input string,
    /// NormalizeBaseUrl shall return a result that does not end with '/'.
    /// </summary>
    [Property]
    public bool Result_never_ends_with_trailing_slash(string input)
    {
        // Skip null and whitespace-only strings (those return empty string)
        if (string.IsNullOrWhiteSpace(input))
            return true;

        var result = CloudlogUrlHelper.NormalizeBaseUrl(input);

        return !result.EndsWith('/');
    }

    /// <summary>
    /// Property 29: URL Whitespace Idempotence.
    ///
    /// For any URL string, NormalizeBaseUrl(url) shall equal
    /// NormalizeBaseUrl("  " + url + "  ") — leading and trailing whitespace
    /// does not affect the result.
    /// </summary>
    [Property]
    public bool Leading_trailing_whitespace_does_not_affect_result(string input)
    {
        // For null, both calls return empty string
        if (input is null)
            return true;

        var resultDirect = CloudlogUrlHelper.NormalizeBaseUrl(input);
        var resultPadded = CloudlogUrlHelper.NormalizeBaseUrl("  " + input + "  ");

        return resultDirect == resultPadded;
    }

    /// <summary>
    /// Property 30: URL No Double Protocol.
    ///
    /// For any input string that already contains "://",
    /// NormalizeBaseUrl shall not prepend an additional "https://".
    /// </summary>
    [Property]
    public bool Input_with_protocol_does_not_get_double_prepended(string input)
    {
        // Skip null and whitespace-only strings (those return empty string)
        if (string.IsNullOrWhiteSpace(input))
            return true;

        // Only test inputs that already contain "://"
        if (!input.Contains("://"))
            return true;

        var result = CloudlogUrlHelper.NormalizeBaseUrl(input);

        // Result should not start with "https://" followed by another "://" nearby
        // (i.e., the original protocol should be preserved, not doubled)
        return !result.StartsWith("https://https://", StringComparison.Ordinal)
            && !result.StartsWith("https://http://", StringComparison.Ordinal);
    }
}
