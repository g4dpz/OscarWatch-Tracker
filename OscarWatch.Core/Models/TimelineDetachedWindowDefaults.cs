namespace OscarWatch.Core.Models;

public static class TimelineDetachedWindowDefaults
{
    public const int DefaultWidth = 960;
    public const int DefaultHeight = 220;
    public const int MinWidth = 480;
    public const int MinHeight = 140;

    public static bool TryGetSavedSize(AppSettings? settings, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (settings?.TimelineDetachedWindowWidth is not int w || settings.TimelineDetachedWindowHeight is not int h)
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
        if (settings?.TimelineDetachedWindowX is not int savedX || settings.TimelineDetachedWindowY is not int savedY)
            return false;

        x = savedX;
        y = savedY;
        return true;
    }
}
