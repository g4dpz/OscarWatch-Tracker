using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IQsoLogbookRepository
{
    string DatabasePath { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QsoLogbook>> ListLogbooksAsync(CancellationToken cancellationToken = default);

    Task<QsoLogbook> CreateLogbookAsync(QsoLogbookCreateRequest request, CancellationToken cancellationToken = default);

    Task DeleteLogbookAsync(long logbookId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QsoRecord>> ListQsosAsync(long logbookId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QsoRecord>> SearchQsosByCallAsync(
        long logbookId,
        string callPrefix,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<QsoRecord?> FindLatestQsoForCallAsync(
        long logbookId,
        string call,
        CancellationToken cancellationToken = default);

    Task<QsoRecord> AddQsoAsync(QsoRecordCreateRequest request, CancellationToken cancellationToken = default);

    Task<QsoRecord> UpdateQsoAsync(QsoRecordUpdateRequest request, CancellationToken cancellationToken = default);

    Task DeleteQsoAsync(long qsoId, CancellationToken cancellationToken = default);
}
