using OscarWatch.Core.Models;
using OscarWatch.Core.Qrz;

namespace OscarWatch.Core.Services;

public interface IQrzCallbookService
{
    bool CanLookup(QrzSettings? settings);

    Task<QrzConnectionTestResult> TestConnectionAsync(
        QrzSettings settings,
        CancellationToken cancellationToken = default);

    Task<QrzCallbookEntry?> LookupAsync(
        QrzSettings settings,
        string callsign,
        CancellationToken cancellationToken = default);
}
