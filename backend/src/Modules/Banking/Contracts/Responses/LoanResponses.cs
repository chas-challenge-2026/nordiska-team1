namespace Nordiska.Modules.Banking.Contracts.Responses;

/// <summary>
/// Response model representing a customer loan.
/// </summary>
/// <param name="Id">The unique identifier of the loan.</param>
/// <param name="CustomerId">The customer identifier owning the loan.</param>
/// <param name="LoanNumber">The loan number.</param>
/// <param name="Type">The loan type ("personal" or "mortgage").</param>
/// <param name="PrincipalAmount">The amount originally borrowed.</param>
/// <param name="OutstandingAmount">What is owed today, including interest accrued up to today.</param>
/// <param name="AccruedInterest">The part of the outstanding amount that is interest not yet booked on the loan.</param>
/// <param name="InterestRate">The annual interest rate as a decimal.</param>
/// <param name="Currency">The loan currency.</param>
/// <param name="Status">The loan status ("active", "repaid", ...).</param>
/// <param name="OpenedAt">The date the loan was opened.</param>
/// <param name="MaturityDate">The date the loan should be fully repaid.</param>
public record LoanResponse(
    long Id,
    long CustomerId,
    string LoanNumber,
    string Type,
    decimal PrincipalAmount,
    decimal OutstandingAmount,
    decimal AccruedInterest,
    decimal InterestRate,
    string Currency,
    string Status,
    DateOnly OpenedAt,
    DateOnly? MaturityDate
);
