using Nordiska.Modules.Faq.Contracts.Responses;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Application;

public interface IFaqSearchLogRepository
{
    Task AddRangeAsync(IReadOnlyCollection<FaqSearchLog> logs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FaqContentGapResponse>> GetContentGapsAsync(string? language, DateTime since, int limit, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
