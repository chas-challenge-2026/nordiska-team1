using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class LoanRepository(BankingDbContext db) : ILoanRepository
{
    public async Task<IEnumerable<Loan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Loans.AsNoTracking()
            .OrderByDescending(l => l.OpenedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Loan>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return await db.Loans.AsNoTracking()
            .Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.OpenedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Loan?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => db.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task CreateWithPayoutAsync(Loan loan, LedgerEntry payout, SavingsAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);
        if (loan.Id != 0) throw new ArgumentException("Only new loan can be created.", nameof(loan));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Loans.Add(loan);
        await BookLedgerEntryAsync(payout, account, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddRepaymentAsync(Loan loan, LedgerEntry repayment, SavingsAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (db.Entry(loan).State == EntityState.Detached)
        {
            db.Loans.Update(loan);
        }
        await BookLedgerEntryAsync(repayment, account, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Same as TransactionService, the stored balance is always the sum of the ledger
    private async Task BookLedgerEntryAsync(LedgerEntry entry, SavingsAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(account);

        var currentBalance = await db.LedgerEntries
            .Where(e => e.AccountId == account.Id && !e.IsPlanned)
            .SumAsync(e => e.Amount, cancellationToken);

        db.LedgerEntries.Add(entry);

        if (db.Entry(account).State == EntityState.Detached)
        {
            db.SavingsAccounts.Attach(account);
        }
        account.Balance = currentBalance + entry.Amount;
        account.UpdatedAt = DateTime.UtcNow;
    }
}
