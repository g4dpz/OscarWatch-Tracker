using System.Diagnostics;

namespace OscarWatch.Help;

/// <summary>Opens the HTML operator help in the default browser.</summary>
public static class HelpLauncher
{
    public static string? LocateHelpDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "help"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "help")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "help"))
        };

        foreach (var dir in candidates)
        {
            if (File.Exists(Path.Combine(dir, "index.html")))
                return dir;
        }

        return null;
    }

    public static bool TryOpenHelp(string? page = null)
    {
        var dir = LocateHelpDirectory();
        if (dir is null)
            return false;

        var file = string.IsNullOrWhiteSpace(page) ? "index.html" : page.Trim();
        if (file.Contains('\\') || file.Contains('/') || file.Contains(".."))
            return false;

        var path = Path.Combine(dir, file);
        if (!File.Exists(path))
            path = Path.Combine(dir, "index.html");

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        return true;
    }
}
