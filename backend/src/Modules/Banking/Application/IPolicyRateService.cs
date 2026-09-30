using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service abstraction for reading the Riksbank policy rate.
/// </summary>
public interface IPolicyRateService
{
    /// <summary>
    /// Retrieves the current policy rate. Results are cached, and the last known value is returned (marked stale)
    /// if the Riksbank can't be reached. Returns null only if no value has ever been fetched.
    /// </summary>
    Task<PolicyRateResponse?> GetAsync(CancellationToken cancellationToken = default);
}
