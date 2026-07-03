namespace OscarWatch.Core.Logbook;

public static class AdifBandHelper
{
    /// <summary>Maps frequency in Hz to an ADIF 3.1 band string (e.g. <c>2m</c>, <c>70cm</c>).</summary>
    public static string FromHz(long hz)
    {
        if (hz <= 0)
            return "";

        var mhz = hz / 1_000_000.0;
        return mhz switch
        {
            >= 2400 and < 2500 => "13cm",
            >= 2300 and < 2400 => "13cm",
            >= 1240 and < 1300 => "23cm",
            >= 902 and < 928 => "33cm",
            >= 420 and < 450 => "70cm",
            >= 222 and < 225 => "1.25m",
            >= 144 and < 148 => "2m",
            >= 50 and < 54 => "6m",
            >= 28 and < 29.7 => "10m",
            >= 24.89 and < 24.99 => "12m",
            >= 21 and < 21.45 => "15m",
            >= 18.068 and < 18.168 => "17m",
            >= 14 and < 14.35 => "20m",
            >= 10.1 and < 10.15 => "30m",
            >= 7 and < 7.3 => "40m",
            >= 5.3 and < 5.4 => "60m",
            >= 3.5 and < 4 => "80m",
            >= 1.8 and < 2 => "160m",
            _ => ""
        };
    }
}
