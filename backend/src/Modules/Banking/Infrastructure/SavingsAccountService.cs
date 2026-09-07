using Nordiska.Modules.Banking.Contracts.Mappers;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Microsoft.Extensions.Logging;

namespace Nordiska.Modules.Banking.Infrastructure;

// Service implementation for managing savings accounts. This class provides methods to retrieve, create, and manage savings accounts.
// ALL METHODS ARE USING IN-MEMORY STORE FOR DEMO PURPOSES!!! TO BE CHANGED TO INTERACT WITH A DATABASE.
public class SavingsAccountService : ISavingsAccountService
{
    private readonly ILogger<SavingsAccountService> _logger;

    // Dependency injection of logger
    public SavingsAccountService(ILogger<SavingsAccountService> logger)
    {
        _logger = logger;
    }

    //Retrive all savings accounts
    //Returns a list of SavingsAccountResponse objects representing all savings accounts
    public Task<IEnumerable<SavingsAccountResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = BankingInMemoryStore.SavingsAccounts.Select(a => a.ToResponse()).ToList();
        return Task.FromResult<IEnumerable<SavingsAccountResponse>>(list);
    }

    //Retrive a savings account by its ID
    //Returns a SavingsAccountResponse object representing the savings account with the specified ID, or null if not found
    public Task<SavingsAccountResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var acc = BankingInMemoryStore.SavingsAccounts.FirstOrDefault(a => a.Id == id);

        if (acc is null)
        {
            // Letting global exception handler convert this to 404 (always happypath)
            throw new KeyNotFoundException($"Savings account with ID {id} was not found.");
        }

        return Task.FromResult(acc.ToResponse());
    }

    // Creates a new savings account based on the provided request
    // Returns a SavingsAccountResponse object representing the newly created savings account
    public Task<SavingsAccountResponse> CreateAsync(OpenSavingsAccountRequest request, CancellationToken cancellationToken = default)
    {
        var entity = request.ToDomain();
        entity.Id = BankingInMemoryStore.NextAccountId();

        // Add to store
        BankingInMemoryStore.SavingsAccounts.Add(entity);

        _logger.LogInformation("Created savings account {Id} for customer {CustomerId}", entity.Id, entity.CustomerId);

        return Task.FromResult(entity.ToResponse());
    }
}
