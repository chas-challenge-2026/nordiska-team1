using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Mappers;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class AccountTypeConfigService : IAccountTypeConfigService, IInterestRateService
{
    private const string CacheKey = "interest-rates:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly IAccountTypeConfigRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AccountTypeConfigService> _logger;

    public AccountTypeConfigService(
        IAccountTypeConfigRepository repo, 
        IMemoryCache cache, 
        ILogger<AccountTypeConfigService> logger)
    {
        _repo = repo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<AccountTypeConfigResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out List<AccountTypeConfigResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var list = await _repo.GetAllAsync(cancellationToken);
        var rates = list.Select(x => x.ToResponse()).ToList();

        _cache.Set(CacheKey, rates, CacheDuration);
        _logger.LogInformation("Loaded {Count} account type interest rates from database and cached for {Duration}", rates.Count, CacheDuration);

        return rates;
    }

    public async Task<AccountTypeConfigResponse> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default)
    {
        var config = await _repo.GetByTypeAsync(accountType, cancellationToken);
        if (config is null)
            throw new NotFoundException($"Account type '{accountType}' was not found.");

        return config.ToResponse();
    }

    public async Task<AccountTypeConfigResponse> CreateAsync(CreateAccountTypeConfigRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedType = request.AccountType.Trim().ToLowerInvariant();
        var existing = await _repo.GetByTypeAsync(normalizedType, cancellationToken);
        if (existing != null)
            throw new ConflictException($"Account type '{normalizedType}' already exists.");

        var rate = request.InterestRate > 1.0m ? request.InterestRate / 100m : request.InterestRate;
        var entity = new AccountTypeConfig
        {
            AccountType = normalizedType,
            InterestRate = rate,
            Description = request.Description ?? string.Empty
        };

        await _repo.CreateAsync(entity, cancellationToken);
        _cache.Remove(CacheKey);
        _logger.LogInformation("Created account type configuration '{AccountType}' with interest rate {Rate} (cache invalidated)", entity.AccountType, entity.InterestRate);
        return entity.ToResponse();
    }

    public async Task<AccountTypeConfigResponse> UpdateAsync(string accountType, UpdateAccountTypeConfigRequest request, CancellationToken cancellationToken = default)
    {
        var config = await _repo.GetByTypeAsync(accountType, cancellationToken);
        if (config is null)
            throw new NotFoundException($"Account type '{accountType}' was not found.");

        if (request.InterestRate.HasValue)
        {
            var rate = request.InterestRate.Value > 1.0m ? request.InterestRate.Value / 100m : request.InterestRate.Value;
            config.InterestRate = rate;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            config.Description = request.Description;
        }

        await _repo.UpdateAsync(config, cancellationToken);
        _cache.Remove(CacheKey);
        _logger.LogInformation("Updated account type configuration '{AccountType}' (InterestRate={Rate}) (cache invalidated)", config.AccountType, config.InterestRate);
        return config.ToResponse();
    }
}
