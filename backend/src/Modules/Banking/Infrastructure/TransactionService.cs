using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
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

    public TransactionService(
        ITransactionRepository txRepo,
        ISavingsAccountRepository accRepo,
        ILogger<TransactionService> logger)
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
            ?? throw new NotFoundException($"Account {accountId} not found.");

        var entries = await _txRepo.QueryAsync(accountId, cancellationToken);
        return entries.Sum(e => e.Amount);
    }

    public async Task<TransactionResponse> ExecuteAsync(TransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAmount(request.Amount);
        ValidateType(request.Type, allowTransfer: false);
        var isWithdrawal = !string.Equals(request.Type, "deposit", StringComparison.OrdinalIgnoreCase);
        var entry = new LedgerEntry
        {
            AccountId = request.AccountId,
            Type = isWithdrawal ? "withdrawal" : "deposit",
            Amount = isWithdrawal ? -request.Amount : request.Amount,
            Label = request.Label,
            CreatedAt = DateTime.UtcNow
        };
        entry.Id = await _txRepo.CreateLedgerEntry(entry, cancellationToken);
        _logger.LogInformation("Executed verified ledger transaction {TxId} on account {AccountId} type={Type} amount={Amount}",
            entry.Id, entry.AccountId, entry.Type, entry.Amount);
        return ToResponse(entry);
    }

    public async Task<TransactionResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAmount(request.Amount);
        if (request.SourceAccountId == request.TargetAccountId)
            throw new ArgumentException("Source and target accounts cannot be the same.", nameof(request.TargetAccountId));

        var entry = await _txRepo.CreateTransferAsync(request, cancellationToken);
        _logger.LogInformation("Executed funds transfer from account {Source} to {Target} amount={Amount}",
            request.SourceAccountId, request.TargetAccountId, request.Amount);
        return ToResponse(entry);
    }

    public async Task<TransactionResponse> CreatePlannedAsync(PlannedTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePlannedRequest(request);
        _ = await _accRepo.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {request.AccountId} was not found.");
        if (request.TargetAccountId.HasValue)
            _ = await _accRepo.GetByIdAsync(request.TargetAccountId.Value, cancellationToken)
                ?? throw new NotFoundException($"Target account {request.TargetAccountId.Value} was not found.");

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
        entry.Id = await _txRepo.CreateAsync(entry, cancellationToken);
        _logger.LogInformation("Created planned transaction {TxId} for account {AccountId} on {PlannedDate}",
            entry.Id, entry.AccountId, entry.PlannedDate);
        return ToResponse(entry);
    }

    public Task<bool> CancelPlannedAsync(long id, CancellationToken cancellationToken = default)
        => _txRepo.DeleteAsync(id, cancellationToken);

    public async Task<TransactionResponse?> ProcessPlannedTransactionAsync(long ledgerEntryId, CancellationToken cancellationToken = default)
    {
        var entry = await _txRepo.ProcessPlannedTransactionAsync(ledgerEntryId, DateTime.UtcNow, cancellationToken);
        if (entry is null)
        {
            _logger.LogDebug("Planned transaction {TxId} was not executed.", ledgerEntryId);
            return null;
        }
        _logger.LogInformation("Processed planned transaction {PlanId} as ledger transaction {TxId}.", ledgerEntryId, entry.Id);
        return ToResponse(entry);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("The amount must be positive and have at most two decimal places.", nameof(amount));
    }

    private static void ValidateType(string type, bool allowTransfer)
    {
        if (!string.Equals(type, "deposit", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(type, "withdrawal", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(type, "withdraw", StringComparison.OrdinalIgnoreCase)
            && !(allowTransfer && string.Equals(type, "transfer", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Invalid transaction type '{type}'.", nameof(type));
    }

    private static void ValidatePlannedRequest(PlannedTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAmount(request.Amount);
        ValidateType(request.Type, allowTransfer: true);
        if (request.PlannedDate.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("Planned date cannot be in the past.", nameof(request.PlannedDate));
        if (string.Equals(request.Type, "transfer", StringComparison.OrdinalIgnoreCase) && !request.TargetAccountId.HasValue)
            throw new ArgumentException("A planned transfer must have a target account.", nameof(request.TargetAccountId));
        if (request.TargetAccountId == request.AccountId)
            throw new ArgumentException("Source and target accounts cannot be the same.", nameof(request.TargetAccountId));
        if (!string.IsNullOrWhiteSpace(request.Repeating)
            && request.Repeating.Trim().ToLowerInvariant() is not ("week" or "month" or "year"))
            throw new ArgumentException("Repeating must be week, month, or year.", nameof(request.Repeating));
    }

    public async Task<IEnumerable<TransactionResponse>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var entries = await _txRepo.GetByCorrelationIdAsync(correlationId, cancellationToken);
        return entries.Select(ToResponse);
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
            l.Repeating,
            l.CorrelationId
        );
}