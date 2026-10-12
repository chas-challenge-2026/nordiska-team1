using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public sealed record RequestAccountStatementResult(
    long AccountStatementId,
    long JobId,
    string Status,
    DateTimeOffset CreatedAt);

public interface IAccountStatementService
{
    Task<RequestAccountStatementResult> RequestAsync(
        long customerId,
        long accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);
}

public sealed class AccountStatementService
    : IAccountStatementService
{
    private const string SchemaVersion = "1.0";

    private readonly IAccountStatementSourceQuery _sourceQuery;
    private readonly IAccountStatementRepository _repository;
    private readonly IAccountStatementPayloadHasher _hasher;
    private readonly TimeProvider _timeProvider;

    public AccountStatementService(
        IAccountStatementSourceQuery sourceQuery,
        IAccountStatementRepository repository,
        IAccountStatementPayloadHasher hasher,
        TimeProvider timeProvider)
    {
        _sourceQuery = sourceQuery;
        _repository = repository;
        _hasher = hasher;
        _timeProvider = timeProvider;
    }

    public async Task<RequestAccountStatementResult> RequestAsync(
        long customerId,
        long accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(fromDate, toDate);

        InFlightAccountStatement? inFlight =
            await _repository.GetInFlightAsync(
                customerId,
                accountId,
                fromDate,
                toDate,
                cancellationToken);

        if (inFlight is not null)
        {
            return MapResult(
                inFlight.Statement,
                inFlight.Job);
        }

        AccountStatementSource? source =
            await _sourceQuery.GetAsync(
                customerId,
                accountId,
                fromDate,
                toDate,
                cancellationToken);

        if (source is null)
        {
            throw new KeyNotFoundException(
                "Kontot finns inte eller tillhör inte den inloggade kunden.");
        }

        long closingBalanceMinor = ToMinor(
            source.CurrentBalance - source.AmountAfterPeriod);

        long periodMovementMinor = source.Entries
            .Sum(entry => ToMinor(entry.Amount));

        long openingBalanceMinor = checked(
            closingBalanceMinor - periodMovementMinor);

        long runningBalanceMinor = openingBalanceMinor;
        var entries = new List<AccountStatementEntryData>(
            source.Entries.Count);

        for (int index = 0; index < source.Entries.Count; index++)
        {
            AccountStatementSourceEntry sourceEntry =
                source.Entries[index];

            long amountMinor = ToMinor(sourceEntry.Amount);
            runningBalanceMinor = checked(
                runningBalanceMinor + amountMinor);

            entries.Add(
                new AccountStatementEntryData(
                    SequenceNumber: index + 1,
                    SourceLedgerEntryId:
                        sourceEntry.SourceLedgerEntryId,
                    BookedAt: sourceEntry.BookedAt,
                    Type: sourceEntry.Type,
                    Description: sourceEntry.Description,
                    AmountMinor: amountMinor,
                    BalanceAfterMinor: runningBalanceMinor));
        }

        DateTimeOffset snapshotAt =
            _timeProvider.GetUtcNow();

        var hashPayload =
            new AccountStatementHashPayload(
                source.CustomerId,
                source.AccountId,
                fromDate,
                toDate,
                source.CustomerName,
                source.AccountNumber,
                source.AccountName,
                source.Currency,
                openingBalanceMinor,
                closingBalanceMinor,
                snapshotAt,
                SchemaVersion,
                entries.Select(entry =>
                        new AccountStatementEntryHashPayload(
                            entry.SequenceNumber,
                            entry.SourceLedgerEntryId,
                            entry.BookedAt,
                            entry.Type,
                            entry.Description,
                            entry.AmountMinor,
                            entry.BalanceAfterMinor))
                    .ToArray());

        string payloadHash = _hasher.Compute(hashPayload);

        AccountStatement statement =
            AccountStatement.Create(
                source.CustomerId,
                source.AccountId,
                fromDate,
                toDate,
                source.CustomerName,
                source.AccountNumber,
                source.AccountName,
                source.Currency,
                openingBalanceMinor,
                closingBalanceMinor,
                snapshotAt,
                SchemaVersion,
                payloadHash,
                entries);

        (AccountStatement savedStatement, AccountStatementJob job) =
            await _repository.CreateStatementAndJobAsync(
                statement,
                snapshotAt,
                cancellationToken);

        return MapResult(savedStatement, job);
    }

    private void ValidatePeriod(
        DateOnly fromDate,
        DateOnly toDate)
    {
        if (fromDate == default || toDate == default)
        {
            throw new ArgumentException(
                "FromDate and ToDate are required.");
        }

        if (fromDate > toDate)
        {
            throw new ArgumentException(
                "FromDate must be before or equal to ToDate.");
        }

        DateOnly today = DateOnly.FromDateTime(
            _timeProvider.GetUtcNow().UtcDateTime);

        if (toDate > today)
        {
            throw new ArgumentException(
                "ToDate must not be in the future.");
        }

        if (toDate >= fromDate.AddMonths(12))
        {
            throw new ArgumentException(
                "The account statement period must not exceed twelve months.");
        }
    }

    private static long ToMinor(decimal amount)
    {
        decimal rounded = decimal.Round(
            amount * 100m,
            0,
            MidpointRounding.AwayFromZero);

        return checked((long)rounded);
    }

    private static RequestAccountStatementResult MapResult(
        AccountStatement statement,
        AccountStatementJob job)
    {
        return new RequestAccountStatementResult(
            statement.Id,
            job.Id,
            job.Status,
            job.CreatedAt);
    }
}
