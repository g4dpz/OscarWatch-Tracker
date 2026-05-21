using System.Text.Json;

namespace OscarWatch.Tests;

public static class GoldenFixtureLoader
{
    private static readonly Lazy<GoldenFixtures> Cached = new(LoadFromDisk);

    public static GoldenFixtures Load() => Cached.Value;

    private static GoldenFixtures LoadFromDisk()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "radio_golden.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GoldenFixtures>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new GoldenFixtures();
    }
}

public sealed class GoldenFixtures
{
    public List<FrequencyEncodeFixture> FrequencyEncode { get; set; } = [];
    public List<RigSatModeFixture> RigSatMode { get; set; } = [];
    public List<SetupVfosFixture> SetupVfos { get; set; } = [];
    public List<DopplerFixture> Doppler { get; set; } = [];
}

public sealed class FrequencyEncodeFixture
{
    public long Hz { get; set; }
    public string PayloadHex { get; set; } = "";
}

public sealed class RigSatModeFixture
{
    public double DownlinkKHz { get; set; }
    public double UplinkKHz { get; set; }
    public bool UseMainSub { get; set; }
}

public sealed class SetupVfosFixture
{
    public string DownlinkMode { get; set; } = "";
    public int ThresholdHz { get; set; }
    public bool Interactive { get; set; }
}

public sealed class DopplerFixture
{
    public long NominalHz { get; set; }
    public double RangeVelocityMps { get; set; }
    public long RxHz { get; set; }
    public long TxHz { get; set; }
}
