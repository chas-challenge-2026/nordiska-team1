using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service interface for operational message business logic, validation, and in-memory caching.
/// </summary>
public interface IOperationalMessageService
{
    /// <summary>
    /// Retrieves all operational messages (active and inactive) for administration.
    /// </summary>
    Task<IEnumerable<OperationalMessageResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active, unexpired operational messages with caching for public consumers.
    /// </summary>
    Task<IEnumerable<OperationalMessageResponse>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single operational message by ID.
    /// </summary>
    Task<OperationalMessageResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new operational message and invalidates the cache.
    /// </summary>
    Task<OperationalMessageResponse> CreateAsync(CreateOperationalMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fully updates an operational message and invalidates the cache.
    /// </summary>
    Task<OperationalMessageResponse> UpdateAsync(long id, UpdateOperationalMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Partially updates an operational message and invalidates the cache.
    /// </summary>
    Task<OperationalMessageResponse> PatchAsync(long id, PatchOperationalMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an operational message and invalidates the cache.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
