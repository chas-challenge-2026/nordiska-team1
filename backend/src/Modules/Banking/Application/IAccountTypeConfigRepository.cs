using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Repository interface for persisting and querying account type configuration records.
/// </summary>
public interface IAccountTypeConfigRepository
{
    /// <summary>
    /// Retrieves all configured account types from the database.
    /// </summary>
    Task<IEnumerable<AccountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an account type configuration by its normalized key.
    /// </summary>
    Task<AccountTypeConfig?> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new account type configuration entity.
    /// </summary>
    Task CreateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing account type configuration entity.
    /// </summary>
    Task UpdateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical interest rate records for a given account type.
    /// </summary>
    Task<IEnumerable<AccountTypeRateHistory>> GetRateHistoryAsync(string accountType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new rate history record.
    /// </summary>
    Task AddRateHistoryAsync(AccountTypeRateHistory history, CancellationToken cancellationToken = default);
}
