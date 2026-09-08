using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Microsoft.Extensions.Logging;

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
        return entries.Select(l => new TransactionResponse(l.Id, l.AccountId, l.Type, l.Amount, l.CreatedAt));
    }

    public async Task<TransactionResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _txRepo.GetByIdAsync(id, cancellationToken);
        if (item is null) throw new KeyNotFoundException($"Transaction with ID {id} was not found.");
        return new TransactionResponse(item.Id, item.AccountId, item.Type, item.Amount, item.CreatedAt);
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
            CreatedAt = DateTime.UtcNow
        };

        var txId = await _txRepo.CreateAsync(entry, cancellationToken);
        entry.Id = txId;

        _logger.LogInformation("Executed verified ledger transaction {TxId} on account {AccountId} type={Type} amount={Amount}", entry.Id, entry.AccountId, entry.Type, entry.Amount);

        return new TransactionResponse(entry.Id, entry.AccountId, entry.Type, entry.Amount, entry.CreatedAt);
    }
}
