using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request payload to apply for a personal loan.
/// </summary>
/// <param name="Amount">Amount to borrow in SEK.</param>
/// <param name="TermMonths">Loan term in months.</param>
/// <param name="PayoutAccountId">The customer's account that the loan is paid out to.</param>
public record ApplyForLoanRequest(
    [Range(0.01, double.MaxValue)] decimal Amount,
    [Range(1, int.MaxValue)] int TermMonths,
    [Range(1, long.MaxValue)] long PayoutAccountId
);

/// <summary>
/// Request payload to repay a loan.
/// </summary>
/// <param name="Amount">Amount to repay in SEK.</param>
/// <param name="FromAccountId">The customer's account that the repayment is withdrawn from.</param>
public record RepayLoanRequest(
    [Range(0.01, double.MaxValue)] decimal Amount,
    [Range(1, long.MaxValue)] long FromAccountId
);
