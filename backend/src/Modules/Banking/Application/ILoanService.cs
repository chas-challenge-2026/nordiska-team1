using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service abstraction for customer loans.
/// </summary>
public interface ILoanService
{
    /// <summary>
    /// Retrieves all loans.
    /// </summary>
    Task<IEnumerable<LoanResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all loans owned by a customer.
    /// </summary>
    /// <param name="customerId">Customer id.</param>
    Task<IEnumerable<LoanResponse>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a loan by id. Outstanding amount includes interest accrued up to today.
    /// </summary>
    /// <param name="id">Loan id.</param>
    Task<LoanResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a personal loan for a customer and pays it out to one of the customer's accounts.
    /// </summary>
    /// <param name="customerId">Customer applying for the loan.</param>
    /// <param name="request">Loan application.</param>
    Task<LoanResponse> ApplyAsync(long customerId, ApplyForLoanRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Repays part of or the whole loan from one of the customer's accounts.
    /// </summary>
    /// <param name="id">Loan id.</param>
    /// <param name="request">Repayment request.</param>
    Task<LoanResponse> RepayAsync(long id, RepayLoanRequest request, CancellationToken cancellationToken = default);
}
