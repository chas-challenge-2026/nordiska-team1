using System;
using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request to execute a transaction against a savings account.
/// </summary>
/// <param name="AccountId">The target account identifier.</param>
/// <param name="Type">Transaction type (e.g. "deposit" or "withdrawal").</param>
/// <param name="Amount">Amount to transact.</param>
/// <param name="Label">Optional label or note for the transaction (e.g. "Hyra", "Lön").</param>
public record TransactionRequest(
    [Required] long AccountId,
    [Required] string Type,
    [Range(0.01, double.MaxValue)] decimal Amount,
    [StringLength(100)] string? Label = null
);

/// <summary>
/// Request to transfer funds between two accounts.
/// </summary>
/// <param name="SourceAccountId">The origin account identifier to withdraw funds from.</param>
/// <param name="TargetAccountId">The destination account identifier to deposit funds into.</param>
/// <param name="Amount">The amount of money to transfer.</param>
/// <param name="Label">Optional label or category for the transfer (e.g. "Hyra", "Spara").</param>
public record TransferRequest(
    [Required] long SourceAccountId,
    [Required] long TargetAccountId,
    [Range(0.01, double.MaxValue)] decimal Amount,
    [StringLength(100)] string? Label = null
);

/// <summary>
/// Request to create a scheduled or planned future transaction.
/// </summary>
/// <param name="AccountId">The account identifier for the transaction.</param>
/// <param name="Type">Transaction type (e.g. "deposit", "withdrawal", or "transfer").</param>
/// <param name="Amount">Amount for the planned transaction.</param>
/// <param name="PlannedDate">The future execution date and time (UTC).</param>
/// <param name="Label">Optional label or reference note.</param>
/// <param name="TargetAccountId">Optional destination account identifier for transfer operations.</param>
/// <param name="Repeating">Optional repetition interval ("week", "month", "year").</param>
public record PlannedTransactionRequest(
    [Required] long AccountId,
    [Required] string Type,
    [Range(0.01, double.MaxValue)] decimal Amount,
    [Required] DateTime PlannedDate,
    [StringLength(100)] string? Label = null,
    long? TargetAccountId = null,
    string? Repeating = null
);
