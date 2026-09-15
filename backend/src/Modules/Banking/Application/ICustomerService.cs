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
    Task<Customer> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a customer with the provided details. Only non-null parameters will be updated.
    /// </summary>
    /// <param name="id">The id of the customer.</param>
    /// <param name="name">The new name of the customer.</param>
    /// <param name="email">The new email of the customer.</param>
    /// <param name="personalNum">The new personal number of the customer.</param>
    /// <param name="phoneNumber">The new phone number of the customer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a customer profile if they have no active accounts with positive balance.
    /// </summary>
    /// <param name="id">The id of the customer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new customer profile.
    /// </summary>
    /// <param name="name">The customer name.</param>
    /// <param name="email">The customer email.</param>
    /// <param name="personalNum">The customer personal number.</param>
    /// <param name="phoneNumber">Optional customer phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Customer> CreateAsync(string name, string email, string personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default);
}
