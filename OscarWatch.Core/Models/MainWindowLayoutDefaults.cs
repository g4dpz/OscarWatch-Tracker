namespace OscarWatch.Core.Models;

public static class MainWindowLayoutDefaults
{
    public const int DefaultWidth = 1280;
    public const int DefaultHeight = 556;
    public const int MinWidth = 720;
    public const int MinHeight = 320;

    public static bool TryGetSavedSize(AppSettings? settings, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (settings?.MainWindowWidth is not int w || settings.MainWindowHeight is not int h)
            return false;

        if (w < MinWidth || h < MinHeight)
            return false;

        width = w;
        height = h;
        return true;
    }

    public static bool TryGetSavedPosition(AppSettings? settings, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (settings?.MainWindowX is not int savedX || settings.MainWindowY is not int savedY)
            return false;

        x = savedX;
        y = savedY;
        return true;
    }
}
