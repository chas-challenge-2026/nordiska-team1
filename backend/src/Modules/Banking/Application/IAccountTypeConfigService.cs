using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service interface for managing bank account types, interest rates, and caching.
/// </summary>
public interface IAccountTypeConfigService
{
    /// <summary>
    /// Retrieves all active account types and their current interest rates (cached).
    /// </summary>
    Task<IEnumerable<AccountTypeConfigResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single account type configuration by its key.
    /// </summary>
    Task<AccountTypeConfigResponse> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new account type configuration and invalidates the interest rate cache.
    /// </summary>
    Task<AccountTypeConfigResponse> CreateAsync(CreateAccountTypeConfigRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the interest rate or description of an existing account type and invalidates the cache.
    /// </summary>
    Task<AccountTypeConfigResponse> UpdateAsync(string accountType, UpdateAccountTypeConfigRequest request, CancellationToken cancellationToken = default);
}
