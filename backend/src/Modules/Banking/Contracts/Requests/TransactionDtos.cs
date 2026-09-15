namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request to execute a transaction against a savings account.
/// </summary>
/// <param name="AccountId">The target account identifier.</param>
/// <param name="Type">Transaction type (e.g. "deposit" or "withdrawal").</param>
/// <param name="Amount">Amount to transact.</param>
public record TransactionRequest(
    long AccountId,
    string Type,
    decimal Amount
);
