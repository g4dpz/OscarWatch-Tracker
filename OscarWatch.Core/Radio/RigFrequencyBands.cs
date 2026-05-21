namespace OscarWatch.Core.Radio;

/// <summary>Guards CI-V reads so uplink (VHF) is not mistaken for downlink (UHF) on satellite rigs.</summary>
public static class RigFrequencyBands
{
    private const long UhfLowerHz = 300_000_000;

    public static bool IsPlausibleReceiveRead(long referenceReceiveHz, long readHz)
    {
        if (referenceReceiveHz <= 0 || readHz <= 0)
            return true;

        var refUhf = referenceReceiveHz >= UhfLowerHz;
        var readUhf = readHz >= UhfLowerHz;
        return refUhf == readUhf;
    }
}
