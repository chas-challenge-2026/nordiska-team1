using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service abstraction for reading interest rates per account type.
/// </summary>
public interface IInterestRateService
{
    /// <summary>
    /// Retrieves the interest rate for every account type. Results are cached.
    /// </summary>
    Task<IEnumerable<AccountTypeConfigResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
