namespace OscarWatch.Core.Services;

public static class TleAutoUpdate
{
    public const int IntervalHours = 6;

    public static bool ShouldRefreshOnStartup(Models.TleAutoUpdateMode mode) =>
        mode is Models.TleAutoUpdateMode.OnStartup or Models.TleAutoUpdateMode.EverySixHours;
}
