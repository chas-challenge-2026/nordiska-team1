using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request payload to open a new savings account for a customer.
/// </summary>
/// <param name="CustomerId">The unique customer identifier owning the account.</param>
/// <param name="AccountNumber">Optional custom account number (auto-generated if omitted or empty).</param>
/// <param name="AccountType">Type of account (e.g., 'saving', 'checking').</param>
/// <param name="InitialDeposit">Initial deposit amount to fund the account.</param>
/// <param name="InterestRate">Annual interest rate expressed as a decimal (e.g. 0.035 for 3.5%).</param>
/// <param name="AccountName">Optional user-friendly account nickname or label (max 40 chars).</param>
public record OpenSavingsAccountRequest(
    [Required] long CustomerId,
    string AccountNumber = "",
    [Required] string AccountType = "saving",
    [Range(0, double.MaxValue)] decimal InitialDeposit = 0,
    [Range(0, 100.0)] decimal? InterestRate = null,
    [StringLength(40)] string? AccountName = null
);

/// <summary>
/// Request payload to update an existing savings account.
/// </summary>
/// <param name="Id">The unique identifier of the savings account.</param>
/// <param name="AccountType">Optional updated account type.</param>
/// <param name="Balance">Optional updated balance.</param>
/// <param name="InterestRate">Optional updated interest rate.</param>
/// <param name="AccountName">Optional updated account nickname (max 40 chars).</param>
public record UpdateSavingsAccountRequest(
    [Required] long Id,
    string? AccountType,
    decimal? Balance,
    decimal? InterestRate,
    [StringLength(40)] string? AccountName = null
);

/// <summary>
/// Request payload to close a savings account.
/// </summary>
/// <param name="Id">The unique identifier of the savings account to close.</param>
/// <param name="Reason">Reason for closing the account.</param>
public record CloseSavingsAccountRequest(
    [Required] long Id,
    [Required] string Reason
);
