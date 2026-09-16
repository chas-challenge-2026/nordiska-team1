using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Mappers;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;

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
        var accountNumber = string.IsNullOrWhiteSpace(request.AccountNumber)
            ? $"NOR-{Random.Shared.Next(100000, 999999)}"
            : request.AccountNumber.Trim();

        var normalizedRequest = request with { AccountNumber = accountNumber };
        var entity = normalizedRequest.ToDomain();
        await _repo.CreateAsync(entity, cancellationToken);
        _logger.LogInformation("Created savings account {Id} (accountNumber={AccountNumber}) for customer {CustomerId}", entity.Id, entity.AccountNumber, entity.CustomerId);
        return entity.ToResponse();
    }

    public async Task<SavingsAccountResponse> CloseAccountAsync(long id, CancellationToken cancellationToken = default)
    {
        var acc = await _repo.GetByIdAsync(id, cancellationToken);
        if (acc is null)
            throw new KeyNotFoundException($"Savings account with ID {id} was not found.");

        if (acc.Balance > 0)
        {
            throw new InvalidOperationException($"Cannot close account with positive balance ({acc.Balance:N2} SEK). Transfer or withdraw all funds before closing.");
        }

        acc.Status = "closed";
        acc.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(acc, cancellationToken);
        _logger.LogInformation("Closed account {Id} (accountNumber={AccountNumber})", acc.Id, acc.AccountNumber);

        return acc.ToResponse();
    }
}
