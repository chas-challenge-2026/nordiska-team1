using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Repository interface for persisting and querying operational message records.
/// </summary>
public interface IOperationalMessageRepository
{
    /// <summary>
    /// Retrieves all operational messages (both active and inactive).
    /// </summary>
    Task<IEnumerable<OperationalMessage>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all currently active and unexpired operational messages sorted by priority and date.
    /// </summary>
    Task<IEnumerable<OperationalMessage>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an operational message by its ID.
    /// </summary>
    Task<OperationalMessage?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new operational message entity.
    /// </summary>
    Task CreateAsync(OperationalMessage entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing operational message entity.
    /// </summary>
    Task UpdateAsync(OperationalMessage entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an operational message by ID.
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
