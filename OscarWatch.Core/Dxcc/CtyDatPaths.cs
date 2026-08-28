namespace OscarWatch.Core.Dxcc;

public static class CtyDatPaths
{
    /// <summary>
    /// Big CTY zip from country-files.com (everyday logging). Update the dated path when shipping a new bundle.
    /// </summary>
    public const string RemoteBigCtyZipUrl = "https://www.country-files.com/bigcty/download/2026/bigcty-20260803.zip";

    public static string UserCountryFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OscarWatch",
        "cty.dat");

    public static string BundledCountryFilePath(string appBaseDirectory) =>
        Path.Combine(appBaseDirectory, "Assets", "cty.dat");

    public static string BundledEntityMapPath(string appBaseDirectory) =>
        Path.Combine(appBaseDirectory, "Assets", "dxcc-prefix-map.json");
}
