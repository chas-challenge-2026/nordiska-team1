using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

public class TransactionService : ITransactionService
{
    private readonly ILogger<TransactionService> _logger;
    private readonly ITransactionRepository _txRepo;
    private readonly ISavingsAccountRepository _accRepo;

    public TransactionService(ITransactionRepository txRepo, ISavingsAccountRepository accRepo, ILogger<TransactionService> logger)
    {
        _txRepo = txRepo;
        _accRepo = accRepo;
        _logger = logger;
    }

    public async Task<IEnumerable<TransactionResponse>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
    {
        var entries = await _txRepo.QueryAsync(accountId, cancellationToken);
        return entries.Select(ToResponse);
    }

    public async Task<PagedResult<TransactionResponse>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var paged = await _txRepo.QueryPagedAsync(parameters, cancellationToken);
        var mapped = paged.Items.Select(ToResponse).ToList();

        return new PagedResult<TransactionResponse>(
            mapped,
            paged.TotalCount,
            paged.Page,
            paged.PageSize,
            paged.TotalPages,
            paged.HasNextPage,
            paged.HasPreviousPage);
    }

    public async Task<TransactionResponse?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _txRepo.GetByIdAsync(id, cancellationToken);
        if (item is null) return null;
        return ToResponse(item);
    }

    public async Task<decimal> GetBalanceAsync(long accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accRepo.GetByIdAsync(accountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Account {accountId} not found.");

        var entries = await _txRepo.QueryAsync(accountId, cancellationToken);
        return entries.Sum(e => e.Amount);
    }

    public async Task<TransactionResponse> ExecuteAsync(TransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Transaction amount must be greater than zero.", nameof(request.Amount));

        var account = await _accRepo.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Account {request.AccountId} not found.");

        var isWithdrawal = string.Equals(request.Type, "withdrawal", StringComparison.OrdinalIgnoreCase);
        var isDeposit = string.Equals(request.Type, "deposit", StringComparison.OrdinalIgnoreCase);

        if (!isWithdrawal && !isDeposit)
            throw new ArgumentException($"Invalid transaction type '{request.Type}'. Allowed types are 'deposit' and 'withdrawal'.", nameof(request.Type));

        decimal delta;
        if (isWithdrawal)
        {
            var currentBalance = await GetBalanceAsync(request.AccountId, cancellationToken);
            if (currentBalance < request.Amount)
            {
                throw new InvalidOperationException($"Insufficient funds. Current balance is {currentBalance:N2}, requested withdrawal is {request.Amount:N2}.");
            }

            delta = -request.Amount;
        }
        else
        {
            delta = request.Amount;
        }

        var entry = new LedgerEntry
        {
            AccountId = request.AccountId,
            Type = request.Type.ToLowerInvariant(),
            Amount = delta,
            Label = request.Label,
            CreatedAt = DateTime.UtcNow
        };

        var txId = await _txRepo.CreateAsync(entry, cancellationToken);
        entry.Id = txId;

        account.Balance = await GetBalanceAsync(request.AccountId, cancellationToken);
        account.UpdatedAt = DateTime.UtcNow;
        await _accRepo.UpdateAsync(account, cancellationToken);

        _logger.LogInformation("Executed verified ledger transaction {TxId} on account {AccountId} type={Type} amount={Amount}", entry.Id, entry.AccountId, entry.Type, entry.Amount);

        return ToResponse(entry);
    }

    public async Task<TransactionResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Transfer amount must be greater than zero.", nameof(request.Amount));

        if (request.SourceAccountId == request.TargetAccountId)
            throw new ArgumentException("Source and target accounts cannot be the same.", nameof(request.TargetAccountId));

        var sourceAccount = await _accRepo.GetByIdAsync(request.SourceAccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source account {request.SourceAccountId} was not found.");

        var targetAccount = await _accRepo.GetByIdAsync(request.TargetAccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Target account {request.TargetAccountId} was not found.");

        var currentBalance = await GetBalanceAsync(request.SourceAccountId, cancellationToken);
        if (currentBalance < request.Amount)
        {
            throw new InvalidOperationException($"Insufficient funds on source account. Current balance is {currentBalance:N2}, requested transfer is {request.Amount:N2}.");
        }

        var now = DateTime.UtcNow;

        // 1. Withdrawal on source account
        var withdrawalEntry = new LedgerEntry
        {
            AccountId = request.SourceAccountId,
            Type = "transfer",
            Amount = -request.Amount,
            Label = request.Label,
            TargetAccountId = request.TargetAccountId,
            CreatedAt = now
        };
        var withdrawalId = await _txRepo.CreateAsync(withdrawalEntry, cancellationToken);
        withdrawalEntry.Id = withdrawalId;

        // 2. Deposit on target account
        var depositEntry = new LedgerEntry
        {
            AccountId = request.TargetAccountId,
            Type = "transfer",
            Amount = request.Amount,
            Label = request.Label,
            TargetAccountId = request.SourceAccountId,
            CreatedAt = now
        };
        await _txRepo.CreateAsync(depositEntry, cancellationToken);

        // 3. Update cached balance snapshots
        sourceAccount.Balance = await GetBalanceAsync(request.SourceAccountId, cancellationToken);
        sourceAccount.UpdatedAt = now;
        await _accRepo.UpdateAsync(sourceAccount, cancellationToken);

        targetAccount.Balance = await GetBalanceAsync(request.TargetAccountId, cancellationToken);
        targetAccount.UpdatedAt = now;
        await _accRepo.UpdateAsync(targetAccount, cancellationToken);

        _logger.LogInformation("Executed funds transfer from account {Source} to {Target} amount={Amount}", request.SourceAccountId, request.TargetAccountId, request.Amount);

        return ToResponse(withdrawalEntry);
    }

    public async Task<TransactionResponse> CreatePlannedAsync(PlannedTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Planned amount must be greater than zero.", nameof(request.Amount));

        var account = await _accRepo.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Account {request.AccountId} was not found.");

        var entry = new LedgerEntry
        {
            AccountId = request.AccountId,
            Type = request.Type.ToLowerInvariant(),
            Amount = request.Amount,
            Label = request.Label,
            TargetAccountId = request.TargetAccountId,
            IsPlanned = true,
            PlannedDate = request.PlannedDate,
            Repeating = request.Repeating,
            CreatedAt = DateTime.UtcNow
        };

        var txId = await _txRepo.CreateAsync(entry, cancellationToken);
        entry.Id = txId;

        _logger.LogInformation("Created planned transaction {TxId} for account {AccountId} on {PlannedDate}", entry.Id, entry.AccountId, entry.PlannedDate);

        return ToResponse(entry);
    }

    public async Task<bool> CancelPlannedAsync(long id, CancellationToken cancellationToken = default)
    {
        var entry = await _txRepo.GetByIdAsync(id, cancellationToken);
        if (entry is null) return false;

        if (!entry.IsPlanned)
        {
            throw new InvalidOperationException("Cannot cancel an executed transaction. Only planned transactions can be cancelled.");
        }

        return await _txRepo.DeleteAsync(id, cancellationToken);
    }

    private static TransactionResponse ToResponse(LedgerEntry l)
        => new(
            l.Id,
            l.AccountId,
            l.Type,
            l.Amount,
            l.CreatedAt,
            l.Label,
            l.TargetAccountId,
            l.IsPlanned,
            l.PlannedDate,
            l.Repeating
        );
}
