using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Repository abstraction for customer loans.
/// </summary>
public interface ILoanRepository
{
    Task<IEnumerable<Loan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Loan>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default);
    Task<Loan?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new loan together with the payout to the customer's account. Either everything is saved or nothing.
    /// </summary>
    Task CreateWithPayoutAsync(Loan loan, LedgerEntry payout, SavingsAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a repayment on a loan together with the withdrawal from the customer's account. Either everything is saved or nothing.
    /// </summary>
    Task AddRepaymentAsync(Loan loan, LedgerEntry repayment, SavingsAccount account, CancellationToken cancellationToken = default);
}
