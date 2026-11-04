using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.Modules.Faq.Application;

public interface IFaqViewLogRepository
{
    Task AddRangeAsync(IReadOnlyCollection<FaqViewLog> logs, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);

    Task<FaqViewAnalyticsResponse> GetAnalyticsAsync(string? language, DateTime since, int limit, CancellationToken cancellationToken = default);
}
