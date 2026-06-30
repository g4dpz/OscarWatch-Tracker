using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class SatelliteCatalogMatching
{
    /// <summary>
    /// Pre-computed index: maps parenthesised aliases extracted from enabled names to true.
    /// E.g. if enabled contains "RADFXSAT (FOX-1B)", index contains "FOX-1B" → true.
    /// </summary>
    private static Dictionary<string, bool>? _parenthesisIndex;

    /// <summary>
    /// Reference to the enabled set used to build <see cref="_parenthesisIndex"/>.
    /// When the enabled set instance changes, the index is rebuilt.
    /// </summary>
    private static IReadOnlySet<string>? _lastEnabledSet;

    public static bool IsEnabled(SatelliteCatalogEntry satellite, IReadOnlySet<string> enabled)
    {
        // O(1) exact match via case-insensitive HashSet
        if (enabled.Contains(satellite.Name))
            return true;

        // O(1) check: is the satellite name itself a parenthesised alias from an enabled name?
        var index = GetOrBuildParenthesisIndex(enabled);
        if (index.Count > 0 && index.ContainsKey(satellite.Name))
            return true;

        // O(M) substring fallback — preserves existing semantics
        foreach (var name in enabled)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (satellite.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                || name.Contains(satellite.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            if (satellite.Name.Contains($"({name})", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the parenthesis alias index, rebuilding it only if the enabled set instance changes.
    /// </summary>
    private static Dictionary<string, bool> GetOrBuildParenthesisIndex(IReadOnlySet<string> enabled)
    {
        if (ReferenceEquals(_lastEnabledSet, enabled) && _parenthesisIndex is not null)
            return _parenthesisIndex;

        _parenthesisIndex = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        _lastEnabledSet = enabled;

        foreach (var name in enabled)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            var openParen = name.IndexOf('(');
            var closeParen = name.IndexOf(')');
            if (openParen >= 0 && closeParen > openParen + 1)
            {
                var alias = name[(openParen + 1)..closeParen].Trim();
                if (!string.IsNullOrWhiteSpace(alias))
                    _parenthesisIndex[alias] = true;
            }
        }

        return _parenthesisIndex;
    }

    /// <summary>
    /// Clears the cached index. Used by tests to ensure isolation.
    /// </summary>
    internal static void ResetCache()
    {
        _parenthesisIndex = null;
        _lastEnabledSet = null;
    }
}
