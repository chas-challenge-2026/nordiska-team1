using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AccountStatementRepository
    : IAccountStatementRepository
{
    private readonly ReportingDbContext _dbContext;

    public AccountStatementRepository(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AccountStatement?> GetByIdAsync(
        long accountStatementId,
        CancellationToken cancellationToken)
    {
        return _dbContext.AccountStatements
            .AsNoTracking()
            .Include(statement => statement.Entries)
            .SingleOrDefaultAsync(
                statement => statement.Id == accountStatementId,
                cancellationToken);
    }

    public Task<InFlightAccountStatement?> GetInFlightAsync(
        long customerId,
        long accountId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        return (
            from job in _dbContext.AccountStatementJobs.AsNoTracking()
            join statement in _dbContext.AccountStatements.AsNoTracking()
                on job.AccountStatementId equals statement.Id
            where statement.CustomerId == customerId &&
                  statement.AccountId == accountId &&
                  statement.PeriodStart == periodStart &&
                  statement.PeriodEnd == periodEnd &&
                  (job.Status == AccountStatementJobStatuses.Pending ||
                   job.Status == AccountStatementJobStatuses.Processing)
            orderby job.CreatedAt descending, job.Id descending
            select new InFlightAccountStatement(statement, job))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(AccountStatement Statement, AccountStatementJob Job)>
        CreateStatementAndJobAsync(
            AccountStatement statement,
            DateTimeOffset jobCreatedAt,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        _dbContext.AccountStatements.Add(statement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        AccountStatementJob job = AccountStatementJob.Create(
            statement.Id,
            jobCreatedAt);

        _dbContext.AccountStatementJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (statement, job);
    }
}
