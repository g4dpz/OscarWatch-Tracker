using OscarWatch.Core.HamQth;
using OscarWatch.Core.Models;
using OscarWatch.Core.Qrz;

namespace OscarWatch.Core.Services;

public interface IHamQthCallbookService
{
    bool CanLookup(HamQthSettings? settings);

    Task<HamQthConnectionTestResult> TestConnectionAsync(
        HamQthSettings settings,
        CancellationToken cancellationToken = default);

    Task<QrzCallbookEntry?> LookupAsync(
        HamQthSettings settings,
        string callsign,
        CancellationToken cancellationToken = default);
}
