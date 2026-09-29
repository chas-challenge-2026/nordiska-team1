using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class ReportDataBuilder(
    ISavingsAccountService savingsAccountService,
    ICustomerService customerService,
    ITransactionService transactionService)
    : IReportDataBuilder
{
    public async Task<TaxReportData> BuildAsync(
        long customerId,
        long accountId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var account = await savingsAccountService.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"Account with ID {accountId} was not found.");

        var customer = await customerService.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException($"Customer with ID {customerId} was not found.");

        var transactions = (await transactionService.QueryAsync(accountId, cancellationToken)).ToList();
        var yearStartDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEndDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var openingBalance = transactions
            .Where(t => t.CreatedAt < yearStartDate)
            .Sum(t => t.Amount);
        var closingBalance = transactions
            .Where(t => t.CreatedAt <= yearEndDate)
            .Sum(t => t.Amount);
        var interestEarned = Math.Round(closingBalance * account.InterestRate, 2);
        var taxWithheld = Math.Round(interestEarned * 0.30m, 2);

        return new TaxReportData(
            AccountId: account.Id,
            AccountNumber: account.AccountNumber,
            AccountName: account.AccountName ?? "Sparkonto",
            CustomerId: customer.Id,
            CustomerName: customer.Name,
            PersonalNum: customer.PersonalNum,
            Year: year,
            OpeningBalance: openingBalance,
            ClosingBalance: closingBalance,
            InterestRate: account.InterestRate,
            TotalInterestEarned: interestEarned,
            TotalTaxWithheld: taxWithheld,
            Transactions: transactions          
                .OrderByDescending(t => t.CreatedAt)
                .ToList(),
            GeneratedAt: DateTime.UtcNow);
    }
}
