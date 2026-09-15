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
    Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, CancellationToken cancellationToken = default);

    Task<Customer> CreateAsync(string name, string email, string personalNum, CancellationToken cancellationToken = default);
}
