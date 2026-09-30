using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed record InterestRatePeriod(
    decimal Rate,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

public sealed class ReportDataBuilder(
    ISavingsAccountService savingsAccountService,
    ICustomerService customerService,
    ITransactionService transactionService,
    IInterestRateService? interestRateService = null)
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

        IReadOnlyList<InterestRatePeriod>? ratePeriods = null;
        if (interestRateService != null && !string.IsNullOrWhiteSpace(account.AccountType))
        {
            var history = await interestRateService.GetRateHistoryAsync(account.AccountType, cancellationToken);
            if (history != null && history.Count > 0)
            {
                ratePeriods = history
                    .Select(h => new InterestRatePeriod(h.InterestRate, h.EffectiveFromUtc, h.EffectiveToUtc))
                    .ToList();
            }
        }

        var interestEarned = CalculateAccruedInterest(openingBalance, transactions, year, account.InterestRate, ratePeriods);
        var taxWithheld = Math.Round(interestEarned * 0.30m, 2, MidpointRounding.AwayFromZero);

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

    /// <summary>
    /// Calculates the accrued daily interest (Actual/365 or Actual/366 for leap years)
    /// based on the account's historical transactions and variable interest rate periods.
    /// </summary>
    public static decimal CalculateAccruedInterest(
        decimal openingBalance,
        IEnumerable<Nordiska.Modules.Banking.Contracts.Responses.TransactionResponse> transactions,
        int year,
        decimal defaultInterestRate,
        IReadOnlyList<InterestRatePeriod>? ratePeriods = null)
    {
        var mapped = transactions.Select(t => (t.Amount, t.CreatedAt));
        return CalculateAccruedInterestInternal(openingBalance, mapped, year, defaultInterestRate, ratePeriods);
    }

    /// <summary>
    /// Overload for LedgerEntry domain entities.
    /// </summary>
    public static decimal CalculateAccruedInterest(
        decimal openingBalance,
        IEnumerable<Nordiska.Modules.Banking.Domain.LedgerEntry> transactions,
        int year,
        decimal defaultInterestRate,
        IReadOnlyList<InterestRatePeriod>? ratePeriods = null)
    {
        var mapped = transactions.Select(t => (t.Amount, t.CreatedAt));
        return CalculateAccruedInterestInternal(openingBalance, mapped, year, defaultInterestRate, ratePeriods);
    }

    private static decimal CalculateAccruedInterestInternal(
        decimal openingBalance,
        IEnumerable<(decimal Amount, DateTime CreatedAt)> transactions,
        int year,
        decimal defaultInterestRate,
        IReadOnlyList<InterestRatePeriod>? ratePeriods)
    {
        var totalDaysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        var yearStartDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEndDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var yearTransactions = transactions
            .Where(t => t.CreatedAt >= yearStartDate && t.CreatedAt <= yearEndDate)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        var txByDay = yearTransactions
            .GroupBy(t => (t.CreatedAt.Date - yearStartDate.Date).Days)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        decimal accumulatedInterest = 0m;
        decimal currentBalance = openingBalance;

        for (int dayOffset = 0; dayOffset < totalDaysInYear; dayOffset++)
        {
            var currentDayDate = yearStartDate.AddDays(dayOffset);

            if (txByDay.TryGetValue(dayOffset, out var dayChange))
            {
                currentBalance += dayChange;
            }

            if (currentBalance <= 0)
            {
                continue;
            }

            decimal activeRate = defaultInterestRate;
            if (ratePeriods != null && ratePeriods.Count > 0)
            {
                var matchingPeriod = ratePeriods
                    .Where(p => p.EffectiveFromUtc.Date <= currentDayDate &&
                                (p.EffectiveToUtc == null || p.EffectiveToUtc.Value.Date >= currentDayDate))
                    .OrderByDescending(p => p.EffectiveFromUtc)
                    .FirstOrDefault();

                if (matchingPeriod != null)
                {
                    activeRate = matchingPeriod.Rate;
                }
            }

            if (activeRate > 0)
            {
                accumulatedInterest += currentBalance * activeRate / totalDaysInYear;
            }
        }

        return Math.Round(accumulatedInterest, 2, MidpointRounding.AwayFromZero);
    }
}
