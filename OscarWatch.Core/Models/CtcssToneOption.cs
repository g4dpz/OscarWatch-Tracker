using OscarWatch.Core.Display;

namespace OscarWatch.Core.Models;

public sealed class CtcssToneOption
{
    public CtcssToneOption(string role, string label, double hz)
    {
        Role = role;
        Label = label;
        Hz = hz;
    }

    public string Role { get; }

    public string Label { get; }

    public double Hz { get; }

    public string DisplayText => $"{Label} — {FrequencyDisplayFormat.FormatCtcssHz(Hz)}";
}
