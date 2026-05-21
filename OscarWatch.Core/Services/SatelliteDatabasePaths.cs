namespace OscarWatch.Core.Services;

public static class SatelliteDatabasePaths
{
    public static string UserDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OscarWatch",
        "satellite_database.json");

    public static string BundledDatabasePath(string appBaseDirectory) =>
        Path.Combine(appBaseDirectory, "Assets", "satellite_database.json");
}
