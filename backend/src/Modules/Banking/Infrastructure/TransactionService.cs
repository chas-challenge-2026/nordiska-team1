using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Microsoft.Extensions.Logging;

namespace Nordiska.Modules.Banking.Infrastructure;

//THIS CLASS IS USING INMEMORY STORE. CHANGE LATER TO USE DATABASE!!!!!!!!!!!!
public class TransactionService : ITransactionService
{
    private readonly ILogger<TransactionService> _logger;

    // Dependency injection of the logger
    public TransactionService(ILogger<TransactionService> logger)
    {
        _logger = logger;
    }

    // Query transactions, optionally filtered by account ID
    //returns a list of TransactionResponse objects representing the transactions
    public Task<IEnumerable<TransactionResponse>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
    {
        var query = BankingInMemoryStore.Ledger.AsEnumerable();
        if (accountId.HasValue)
            query = query.Where(l => l.AccountId == accountId.Value);

        var results = query.Select(l => new TransactionResponse(l.Id, l.AccountId, l.Type, l.Amount, l.CreatedAt)).ToList();
        return Task.FromResult<IEnumerable<TransactionResponse>>(results);
    }

    // Retrieve a transaction by its ID
    // Returns a TransactionResponse object representing the transaction with the specified ID, or throws an exception if not found
    public Task<TransactionResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = BankingInMemoryStore.Ledger.FirstOrDefault(l => l.Id == id);

        if (item is null)
        {
            throw new KeyNotFoundException($"Transaction with ID {id} was not found.");
        }

        return Task.FromResult(new TransactionResponse(item.Id, item.AccountId, item.Type, item.Amount, item.CreatedAt));
    }

    // Execute a transaction (deposit or withdrawal) on a savings account
    // Returns a TransactionResponse object representing the executed transaction
    public Task<TransactionResponse> ExecuteAsync(TransactionRequest request, CancellationToken cancellationToken = default)
    {
        var account = BankingInMemoryStore.SavingsAccounts.FirstOrDefault(a => a.Id == request.AccountId)
            ?? throw new KeyNotFoundException($"Account {request.AccountId} not found.");

        var isWithdrawal = string.Equals(request.Type, "withdrawal", StringComparison.OrdinalIgnoreCase);
        var delta = isWithdrawal ? -request.Amount : request.Amount;

        if (isWithdrawal && account.Balance < request.Amount)
            throw new InvalidOperationException("Insufficient funds.");

        // Update balance
        account.Balance += delta;

        var entry = new LedgerEntry
        {
            Id = BankingInMemoryStore.NextLedgerId(),
            AccountId = request.AccountId,
            Type = request.Type,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow
        };

        BankingInMemoryStore.Ledger.Add(entry);

        _logger.LogInformation("Executed transaction {TxId} on account {AccountId} type={Type} amount={Amount}", entry.Id, entry.AccountId, entry.Type, entry.Amount);

        var response = new TransactionResponse(entry.Id, entry.AccountId, entry.Type, entry.Amount, entry.CreatedAt);
        return Task.FromResult(response);
    }
}
