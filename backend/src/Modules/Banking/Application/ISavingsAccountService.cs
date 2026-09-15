using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service abstraction for managing savings accounts.
/// </summary>
public interface ISavingsAccountService
{
    /// <summary>
    /// Retrieves all savings accounts.
    /// </summary>
    Task<IEnumerable<SavingsAccountResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a savings account by id.
    /// </summary>
    /// <param name="id">Account id.</param>
    Task<SavingsAccountResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates (opens) a new savings account.
    /// </summary>
    /// <param name="request">Open savings account request.</param>
    Task<SavingsAccountResponse> CreateAsync(OpenSavingsAccountRequest request, CancellationToken cancellationToken = default);
}
