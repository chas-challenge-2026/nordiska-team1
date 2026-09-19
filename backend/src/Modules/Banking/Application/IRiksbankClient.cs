namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Typed client for the Riksbank SWEA API.
/// </summary>
public interface IRiksbankClient
{
    /// <summary>
    /// Retrieves the latest published policy rate. Throws if the Riksbank can't be reached or returns something unexpected.
    /// </summary>
    Task<PolicyRateObservation> GetLatestPolicyRateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A single policy rate observation as published by the Riksbank. The value is in percent (1.75 = 1.75 %).
/// </summary>
public record PolicyRateObservation(DateOnly Date, decimal ValuePercent);
