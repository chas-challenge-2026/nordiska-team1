using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Application;

public interface IFaqViewLogRepository
{
    Task AddRangeAsync(IReadOnlyCollection<FaqViewLog> logs, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
