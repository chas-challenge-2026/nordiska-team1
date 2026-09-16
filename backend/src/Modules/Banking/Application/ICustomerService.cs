using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service abstraction for managing customers.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Retrieves a customer by id.
    /// </summary>
    /// <param name="id">The id of the customer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Customer> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a customer with the provided details. Only non-null parameters will be updated.
    /// </summary>
    Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Partially updates customer profile fields (e.g. email or phone number).
    /// </summary>
    Task<Customer> PatchProfileAsync(long id, string? name, string? email, string? phoneNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a customer profile if they have no active accounts with positive balance.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new customer profile.
    /// </summary>
    Task<Customer> CreateAsync(string name, string email, string personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default);
}
