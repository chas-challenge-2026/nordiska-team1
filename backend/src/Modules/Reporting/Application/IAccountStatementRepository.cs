using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public sealed record InFlightAccountStatement(
    AccountStatement Statement,
    AccountStatementJob Job);

public interface IAccountStatementRepository
{
    Task<AccountStatement?> GetByIdAsync(
        long accountStatementId,
        CancellationToken cancellationToken);

    Task<InFlightAccountStatement?> GetInFlightAsync(
        long customerId,
        long accountId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken);

    Task<(AccountStatement Statement, AccountStatementJob Job)>
        CreateStatementAndJobAsync(
            AccountStatement statement,
            DateTimeOffset jobCreatedAt,
            CancellationToken cancellationToken);
}
