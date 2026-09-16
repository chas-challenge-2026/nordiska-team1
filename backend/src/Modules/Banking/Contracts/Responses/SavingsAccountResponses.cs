using System;

namespace Nordiska.Modules.Banking.Contracts.Responses;

/// <summary>
/// Response model representing a customer savings account.
/// </summary>
/// <param name="Id">The unique identifier of the savings account.</param>
/// <param name="CustomerId">The customer identifier owning the account.</param>
/// <param name="AccountNumber">The formatted account number (e.g. "NOR-10001").</param>
/// <param name="AccountType">The type of account (e.g. "saving", "checking").</param>
/// <param name="Balance">The verified current balance calculated from immutable ledger entries.</param>
/// <param name="InterestRate">The annual interest rate as a decimal.</param>
/// <param name="CreatedAt">The timestamp when the account was opened (UTC).</param>
/// <param name="AccountName">Optional user-friendly nickname for the account.</param>
/// <param name="UpdatedAt">Optional timestamp when the account details were last modified (UTC).</param>
/// <param name="Status">The account status ("active" or "closed").</param>
public record SavingsAccountResponse(
    long Id,
    long CustomerId,
    string AccountNumber,
    string AccountType,
    decimal Balance,
    decimal InterestRate,
    DateTime CreatedAt,
    string? AccountName = null,
    DateTime? UpdatedAt = null,
    string Status = "active"
)
{
    /// <summary>
    /// Alias for AccountType to support frontend contracts.
    /// </summary>
    public string Type => AccountType;
}
