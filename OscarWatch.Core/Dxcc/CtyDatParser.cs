using System.Globalization;
using System.Text.RegularExpressions;

namespace OscarWatch.Core.Dxcc;

/// <summary>Parses AD1C cty.dat (Big CTY / contest CTY) into prefix and exact-call tables.</summary>
public static class CtyDatParser
{
    private static readonly Regex OverridePattern = new(
        @"(\(\d+\))|(\[\d+\])|(<\d+/\d+>)|(\{[A-Za-z]+\})|(~\-?\d+(?:\.\d+)?~)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CtyDatDatabase Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var entities = new List<(CtyEntity Entity, string AliasBlock)>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || !line.Contains(':', StringComparison.Ordinal))
                continue;

            if (!TryParseEntityHeader(line, out var entity, out var firstAliases))
                continue;

            var aliases = new StringBuilderLike(firstAliases);
            while (!aliases.EndsWithSemicolon && i + 1 < lines.Length)
            {
                i++;
                aliases.Append(lines[i].Trim());
            }

            entities.Add((entity, aliases.ToString()));
        }

        var exact = new Dictionary<string, CtyEntity>(StringComparer.Ordinal);
        var prefixes = new List<CtyPrefixEntry>();

        foreach (var (entity, aliasBlock) in entities)
        {
            foreach (var raw in SplitAliases(aliasBlock))
            {
                var token = OverridePattern.Replace(raw, "").Trim();
                if (token.Length == 0)
                    continue;

                var isExact = token.StartsWith('=');
                if (isExact)
                    token = token[1..];

                token = token.Trim().ToUpperInvariant();
                if (token.Length == 0)
                    continue;

                if (isExact)
                {
                    exact.TryAdd(token, entity);
                    continue;
                }

                prefixes.Add(new CtyPrefixEntry
                {
                    Prefix = token,
                    Entity = entity,
                    IsExactCall = false
                });
            }

            // Ensure the primary prefix is searchable even if omitted from aliases.
            var primary = entity.PrimaryPrefix.ToUpperInvariant();
            if (!prefixes.Exists(p => p.Entity == entity && p.Prefix == primary))
            {
                prefixes.Add(new CtyPrefixEntry
                {
                    Prefix = primary,
                    Entity = entity,
                    IsExactCall = false
                });
            }
        }

        prefixes.Sort(static (a, b) =>
        {
            var byLen = b.Prefix.Length.CompareTo(a.Prefix.Length);
            return byLen != 0 ? byLen : string.CompareOrdinal(a.Prefix, b.Prefix);
        });

        return new CtyDatDatabase
        {
            PrefixesByLengthDescending = prefixes,
            ExactCalls = exact
        };
    }

    public static CtyDatDatabase ParseFile(string path) =>
        Parse(File.ReadAllText(path));

    private static bool TryParseEntityHeader(string line, out CtyEntity entity, out string firstAliases)
    {
        entity = null!;
        firstAliases = "";

        var parts = line.Split(':');
        if (parts.Length < 8)
            return false;

        var name = parts[0].Trim();
        if (name.Length == 0)
            return false;

        var primaryRaw = parts[7].Trim();
        var isWaeOnly = primaryRaw.StartsWith('*');
        if (isWaeOnly)
            primaryRaw = primaryRaw[1..];

        var primary = primaryRaw.Trim().ToUpperInvariant();
        if (primary.Length == 0)
            return false;

        _ = int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cq);
        _ = int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var itu);

        entity = new CtyEntity
        {
            Name = name,
            PrimaryPrefix = primary,
            IsWaeOnly = isWaeOnly,
            CQZone = cq,
            ITUZone = itu,
            Continent = parts[3].Trim().ToUpperInvariant()
        };

        // Remaining fields after the 8th colon-delimited header field may start aliases on the same line.
        if (parts.Length > 8)
            firstAliases = string.Join(':', parts.Skip(8)).Trim();
        else
            firstAliases = "";

        return true;
    }

    private static IEnumerable<string> SplitAliases(string block)
    {
        var cleaned = block.Trim().TrimEnd(';');
        if (cleaned.Length == 0)
            yield break;

        foreach (var part in cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length > 0)
                yield return part;
        }
    }

    private sealed class StringBuilderLike
    {
        private readonly System.Text.StringBuilder _sb;

        public StringBuilderLike(string seed) => _sb = new System.Text.StringBuilder(seed);

        public bool EndsWithSemicolon
        {
            get
            {
                for (var i = _sb.Length - 1; i >= 0; i--)
                {
                    if (char.IsWhiteSpace(_sb[i]))
                        continue;
                    return _sb[i] == ';';
                }

                return false;
            }
        }

        public void Append(string value) => _sb.Append(value);

        public override string ToString() => _sb.ToString();
    }
}
