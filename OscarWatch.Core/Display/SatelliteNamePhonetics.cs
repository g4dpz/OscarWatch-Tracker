namespace OscarWatch.Core.Display;

public static class SatelliteNamePhonetics
{
    private static readonly IReadOnlyDictionary<char, string> LetterWords =
        new Dictionary<char, string>
        {
            ['A'] = "Alpha",
            ['B'] = "Bravo",
            ['C'] = "Charlie",
            ['D'] = "Delta",
            ['E'] = "Echo",
            ['F'] = "Foxtrot",
            ['G'] = "Golf",
            ['H'] = "Hotel",
            ['I'] = "India",
            ['J'] = "Juliet",
            ['K'] = "Kilo",
            ['L'] = "Lima",
            ['M'] = "Mike",
            ['N'] = "November",
            ['O'] = "Oscar",
            ['P'] = "Papa",
            ['Q'] = "Quebec",
            ['R'] = "Romeo",
            ['S'] = "Sierra",
            ['T'] = "Tango",
            ['U'] = "Uniform",
            ['V'] = "Victor",
            ['W'] = "Whiskey",
            ['X'] = "X-ray",
            ['Y'] = "Yankee",
            ['Z'] = "Zulu"
        };

    private static readonly IReadOnlyDictionary<char, string> DigitWords =
        new Dictionary<char, string>
        {
            ['0'] = "Zero",
            ['1'] = "One",
            ['2'] = "Two",
            ['3'] = "Three",
            ['4'] = "Four",
            ['5'] = "Five",
            ['6'] = "Six",
            ['7'] = "Seven",
            ['8'] = "Eight",
            ['9'] = "Nine"
        };

    public static string ToPhonetic(string satelliteName)
    {
        if (string.IsNullOrWhiteSpace(satelliteName))
            return "Satellite";

        var trimmed = satelliteName.Trim();
        if (IsInternationalSpaceStation(trimmed))
            return "International Space Station";

        var words = new List<string>();
        foreach (var ch in trimmed)
        {
            if (char.IsLetter(ch))
            {
                if (LetterWords.TryGetValue(char.ToUpperInvariant(ch), out var word))
                    words.Add(word);
            }
            else if (char.IsDigit(ch) && DigitWords.TryGetValue(ch, out var digit))
            {
                words.Add(digit);
            }
        }

        return words.Count == 0 ? trimmed : string.Join(' ', words);
    }

    private static bool IsInternationalSpaceStation(string name)
    {
        if (name.Equals("ISS", StringComparison.OrdinalIgnoreCase))
            return true;

        return name.Length > 3
            && name.StartsWith("ISS", StringComparison.OrdinalIgnoreCase)
            && !char.IsLetter(name[3]);
    }

    public static string FormatRisingAnnouncement(string satelliteName) =>
        $"{ToPhonetic(satelliteName)} is rising";

    public static string SampleAnnouncementText => FormatRisingAnnouncement("AO-07");
}
