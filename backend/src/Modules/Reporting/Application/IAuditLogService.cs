using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public interface IAuditLogService
{
    Task<AuditEntry> LogAsync(string action, long? userId, string details, CancellationToken cancellationToken = default);
    
    bool VerifySignature(AuditEntry entry);
    
    Task<(IReadOnlyList<AuditEntry> Entries, int TotalCount)> GetEntriesAsync(
        long? userId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<AuditEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
