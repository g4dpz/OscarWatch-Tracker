using OscarWatch.Core.Models;

namespace OscarWatch.Core.Tle;

public static class TleParser
{
    public static IReadOnlyList<SatelliteCatalogEntry> ParseCatalog(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var entries = new List<SatelliteCatalogEntry>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.StartsWith('1') || line.Length < 60)
                continue;

            if (i == 0)
                continue;

            var nameLine = lines[i - 1];
            if (nameLine.StartsWith('1') || nameLine.StartsWith('2'))
                continue;

            if (i + 1 >= lines.Length || !lines[i + 1].StartsWith('2'))
                continue;

            var line1 = line;
            var line2 = lines[i + 1];
            var name = nameLine.Trim();

            var norad = line1.Length >= 7 ? line1[2..7].Trim() : "";
            DateTime? epoch = null;
            if (line1.Length >= 32)
            {
                try
                {
                    var epochYear = int.Parse(line1.AsSpan(18, 2));
                    var epochDay = double.Parse(line1.AsSpan(20, 12));
                    var year = epochYear < 57 ? 2000 + epochYear : 1900 + epochYear;
                    epoch = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddDays(epochDay - 1);
                }
                catch
                {
                    // ignore parse errors
                }
            }

            entries.Add(new SatelliteCatalogEntry
            {
                Name = name,
                NoradId = norad,
                Line1 = line1,
                Line2 = line2,
                EpochUtc = epoch
            });

            i++;
        }

        return entries;
    }

    public static string SerializeCatalog(IEnumerable<SatelliteCatalogEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in entries)
        {
            sb.AppendLine(e.Name);
            sb.AppendLine(e.Line1);
            sb.AppendLine(e.Line2);
        }
        return sb.ToString();
    }
}
