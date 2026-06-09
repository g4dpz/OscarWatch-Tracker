// Feature: test-coverage-expansion, Property 25: CloudlogResponseParser Never Throws
// Feature: test-coverage-expansion, Property 26: Non-Success Status

using System.Text.Json;
using FsCheck.Xunit;
using OscarWatch.Core.Cloudlog;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 10.6, 10.3**
///
/// Property-based tests verifying that <see cref="CloudlogResponseParser"/>
/// never throws for any input and correctly identifies non-success statuses.
/// </summary>
public class CloudlogResponseParserPropertyTests
{
    /// <summary>
    /// Property 25: CloudlogResponseParser Never Throws.
    ///
    /// For any string input (including null), calling TryParse shall not throw
    /// an exception and shall return a bool.
    /// </summary>
    [Property]
    public bool TryParse_never_throws_for_any_string_input(string? input)
    {
        try
        {
            CloudlogResponseParser.TryParse(input, out _, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Property 26: Non-Success Status.
    ///
    /// For any valid JSON string with a "status" field that is not "success"
    /// (case-insensitive), TryParse shall set the success output parameter to false.
    /// </summary>
    [Property]
    public bool Non_success_status_sets_success_to_false(string rawStatus)
    {
        // Skip null — we need a real status value to embed in JSON
        if (rawStatus is null)
            return true;

        // Skip values that are "success" case-insensitively
        if (string.Equals(rawStatus, "success", System.StringComparison.OrdinalIgnoreCase))
            return true;

        // Build valid JSON with the given status field
        var json = JsonSerializer.Serialize(new { status = rawStatus });

        var result = CloudlogResponseParser.TryParse(json, out var success, out _);

        // TryParse should return true (valid JSON was parsed) and success should be false
        return result && !success;
    }
}
