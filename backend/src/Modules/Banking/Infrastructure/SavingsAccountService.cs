using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database.Errors;
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
    private readonly IAccountTypeConfigRepository? _accountTypeConfigRepo;

    public SavingsAccountService(
        ISavingsAccountRepository repo, 
        ILogger<SavingsAccountService> logger,
        IAccountTypeConfigRepository? accountTypeConfigRepo = null)
    {
        _repo = repo;
        _logger = logger;
        _accountTypeConfigRepo = accountTypeConfigRepo;
    }

    public async Task<IEnumerable<SavingsAccountResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _repo.GetAllAsync(cancellationToken);
        return list.Select(a => a.ToResponse());
    }

    public async Task<IEnumerable<SavingsAccountResponse>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var list = await _repo.GetByCustomerIdAsync(customerId, cancellationToken);
        return list.Select(a => a.ToResponse());
    }

    public async Task<SavingsAccountResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var acc = await _repo.GetByIdAsync(id, cancellationToken);
        if (acc is null)
            throw new NotFoundException($"Savings account with ID {id} was not found.");
        return acc.ToResponse();
    }

    public async Task<SavingsAccountResponse> CreateAsync(OpenSavingsAccountRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = string.IsNullOrWhiteSpace(request.AccountNumber)
            ? $"NOR-{Random.Shared.Next(100000, 999999)}"
            : request.AccountNumber.Trim();

        var normalizedType = NormalizeAccountType(request.AccountType);

        AccountTypeConfig? config = null;
        if (_accountTypeConfigRepo != null)
        {
            config = await _accountTypeConfigRepo.GetByTypeAsync(normalizedType, cancellationToken);
            if (config is null && !string.IsNullOrWhiteSpace(request.AccountType))
            {
                config = await _accountTypeConfigRepo.GetByTypeAsync(request.AccountType.Trim(), cancellationToken);
            }
        }

        // Reject unknown account types explicitly rather than silently coercing
        if (config is null && !IsStandardType(normalizedType))
        {
            throw new NotFoundException($"Account type '{request.AccountType}' is not supported. Supported account types: saving, flex, fix, standard, premium.");
        }

        var finalType = config?.AccountType ?? normalizedType;

        decimal rate;
        if (request.InterestRate.HasValue)
        {
            rate = request.InterestRate.Value > 1.0m ? request.InterestRate.Value / 100m : request.InterestRate.Value;
        }
        else
        {
            rate = config?.InterestRate ?? 0.0250m;
        }

        var normalizedRequest = request with 
        { 
            AccountNumber = accountNumber,
            AccountType = finalType,
            InterestRate = rate
        };

        var entity = normalizedRequest.ToDomain();
        entity.InterestRate = rate;
        await _repo.CreateAsync(entity, cancellationToken);
        _logger.LogInformation("Created savings account {Id} (accountNumber={AccountNumber}, type={AccountType}, rate={Rate}) for customer {CustomerId}", 
            entity.Id, entity.AccountNumber, entity.AccountType, entity.InterestRate, entity.CustomerId);
        return entity.ToResponse();
    }

    private static string NormalizeAccountType(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "saving";
        var lower = rawType.Trim().ToLowerInvariant();
        if (lower == "flex" || lower.Contains("flex")) return "flex";
        if (lower == "fix" || lower.Contains("fix")) return "fix";
        if (lower == "premium" || lower.Contains("premium")) return "premium";
        if (lower == "standard" || lower.Contains("standard")) return "standard";
        if (lower == "saving" || lower == "savings" || lower.Contains("sparkonto") || lower.Contains("saving")) return "saving";
        return lower;
    }

    private static bool IsStandardType(string type)
        => type is "saving" or "flex" or "fix" or "standard" or "premium";

    public async Task<SavingsAccountResponse> CloseAccountAsync(long id, CancellationToken cancellationToken = default)
    {
        var acc = await _repo.GetByIdAsync(id, cancellationToken);
        if (acc is null)
            throw new NotFoundException($"Savings account with ID {id} was not found.");

        if (acc.Balance > 0)
        {
            throw new ConflictException($"Cannot close account with positive balance ({acc.Balance:N2} SEK). Transfer or withdraw all funds before closing.");
        }

        acc.Status = "closed";
        acc.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(acc, cancellationToken);
        _logger.LogInformation("Closed account {Id} (accountNumber={AccountNumber})", acc.Id, acc.AccountNumber);

        return acc.ToResponse();
    }
}
