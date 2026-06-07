using OscarWatch.Core.Hardware;

namespace OscarWatch.Localization;

public static class DualRadioConfigLocalizer
{
    public static string Localize(string code, ILocalizationService localization) => code switch
    {
        DualRadioConfigHelper.MissingDownlinkCode =>
            localization.Get("Settings.Radio.DualIncompleteDownlink"),
        DualRadioConfigHelper.MissingUplinkCode =>
            localization.Get("Settings.Radio.DualIncompleteUplink"),
        DualRadioConfigHelper.MissingBothCode =>
            localization.Get("Settings.Radio.DualIncompleteBoth"),
        _ => ""
    };
}
