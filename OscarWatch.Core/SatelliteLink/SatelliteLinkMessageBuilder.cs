using System.Globalization;
using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Logbook;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.SatelliteLink;

public static class SatelliteLinkMessageBuilder
{
    public static SatelliteLinkMessage BuildEmpty(DateTime timestampUtc) =>
        new()
        {
            TimestampUtc = FormatTimestamp(timestampUtc),
            InRange = false,
            WispDde = WispDdeFormatter.FormatNoSatellite()
        };

    public static SatelliteLinkMessage Build(
        RigTrackingContext context,
        bool onlyWhenInRange,
        DateTime timestampUtc)
    {
        var elevation = context.TrackState.LookAngles?.ElevationDeg ?? double.NegativeInfinity;
        if (onlyWhenInRange && elevation <= 0)
            return BuildEmpty(timestampUtc);

        var radioUpdate = CloudlogRadioMapper.TryCreate(
            context.TrackState.Name,
            context.Mode,
            context.Corrected,
            context.CwUplink,
            context.CwKeepSidebandDownlink);

        if (radioUpdate is null)
            return BuildEmpty(timestampUtc);

        var look = context.TrackState.LookAngles;
        var tracking = look is null
            ? null
            : new SatelliteLinkTrackingInfo
            {
                AzimuthDeg = look.AzimuthDeg,
                ElevationDeg = look.ElevationDeg,
                RangeKm = look.RangeKm,
                RangeRateKmPerSec = look.RangeRateKmPerSec,
                IsSunlit = context.TrackState.IsSunlit
            };

        return new SatelliteLinkMessage
        {
            TimestampUtc = FormatTimestamp(timestampUtc),
            InRange = true,
            Satellite = new SatelliteLinkSatelliteInfo
            {
                Name = radioUpdate.SatelliteName,
                NoradId = context.TrackState.NoradId,
                ModeType = context.Mode.Type
            },
            Frequencies = new SatelliteLinkFrequencyInfo
            {
                UplinkHz = radioUpdate.UplinkHz,
                DownlinkHz = radioUpdate.DownlinkHz,
                UplinkMode = radioUpdate.UplinkMode,
                DownlinkMode = radioUpdate.DownlinkMode,
                NominalUplinkKHz = context.Mode.UplinkKHz,
                NominalDownlinkKHz = context.Mode.DownlinkKHz,
                IsBeaconOnly = context.Mode.IsBeaconOnly
            },
            Bands = new SatelliteLinkBandInfo
            {
                Tx = AdifBandHelper.FromHz(radioUpdate.UplinkHz),
                Rx = AdifBandHelper.FromHz(radioUpdate.DownlinkHz)
            },
            Tracking = tracking,
            DopplerStrategy = MapDopplerStrategy(context.DopplerStrategy),
            WispDde = WispDdeFormatter.FormatFromContext(
                context,
                radioUpdate.UplinkHz,
                radioUpdate.DownlinkHz,
                radioUpdate.UplinkMode,
                radioUpdate.DownlinkMode)
        };
    }

    private static string MapDopplerStrategy(DopplerStrategy strategy) => strategy switch
    {
        DopplerStrategy.DownlinkOnly => "downlinkOnly",
        DopplerStrategy.UplinkOnly => "uplinkOnly",
        _ => "full"
    };

    private static string FormatTimestamp(DateTime timestampUtc) =>
        timestampUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
