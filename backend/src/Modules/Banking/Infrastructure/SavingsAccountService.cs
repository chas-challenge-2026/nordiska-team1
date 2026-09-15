using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Contracts.Mappers;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Banking.Application;

namespace Nordiska.Modules.Banking.Infrastructure;

public class SavingsAccountService : ISavingsAccountService
{
    private readonly ILogger<SavingsAccountService> _logger;
    private readonly ISavingsAccountRepository _repo;

    public SavingsAccountService(ISavingsAccountRepository repo, ILogger<SavingsAccountService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<IEnumerable<SavingsAccountResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _repo.GetAllAsync(cancellationToken);
        return list.Select(a => a.ToResponse());
    }

    public async Task<SavingsAccountResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var acc = await _repo.GetByIdAsync(id, cancellationToken);
        if (acc is null)
            throw new KeyNotFoundException($"Savings account with ID {id} was not found.");
        return acc.ToResponse();
    }

    public async Task<SavingsAccountResponse> CreateAsync(OpenSavingsAccountRequest request, CancellationToken cancellationToken = default)
    {
        var entity = request.ToDomain();
        await _repo.CreateAsync(entity, cancellationToken);
        _logger.LogInformation("Created savings account {Id} for customer {CustomerId}", entity.Id, entity.CustomerId);
        return entity.ToResponse();
    }
}
