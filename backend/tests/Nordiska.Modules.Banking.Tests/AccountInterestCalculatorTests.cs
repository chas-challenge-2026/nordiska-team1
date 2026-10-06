using System;
using System.Collections.Generic;
using Nordiska.Modules.Banking.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Banking.Tests;

public class AccountInterestCalculatorTests
{
    [Fact]
    public void Calculate_MidYearProjection_ReturnsAccurateAccruedYtdAndYearEndTotal()
    {
        // Arrange: 100,000 SEK at 4.0% all year (2026 = 365 days).
        // Today is July 1st (asOfDateUtc = 2026-07-01 12:00:00).
        // Jan 1 to Jul 1 = 182 days (Jan:31, Feb:28, Mar:31, Apr:30, May:31, Jun:30 + Jul 1: 1 = 182 days).
        // Remaining days: 365 - 182 = 183 days (Jul 2 - Dec 31).
        const decimal openingBalance = 100000m;
        const decimal interestRate = 0.04m;
        const int year = 2026;
        var transactions = new List<(decimal Amount, DateTime CreatedAt)>();
        var asOfDate = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var (accruedYtd, estimatedYearEnd) = AccountInterestCalculator.Calculate(
            openingBalance,
            transactions,
            year,
            interestRate,
            rateHistory: null,
            asOfDateUtc: asOfDate);

        // Assert:
        // accruedYtd: 100,000 * 0.04 * (182 / 365) = 1994.52 SEK
        // estimatedYearEnd: 100,000 * 0.04 * (365 / 365) = 4000.00 SEK
        Assert.Equal(1994.52m, accruedYtd);
        Assert.Equal(4000.00m, estimatedYearEnd);
    }

    [Fact]
    public void Calculate_LateDepositOnDec1_ShowsAccurateYtdAndProjectedTotal()
    {
        // Arrange: 0 opening balance.
        // Deposit 100,000 on Dec 1st at 4.0% in 2026 (365 days).
        // Today is Dec 1st (asOfDateUtc = 2026-12-01).
        // Day 334 (Dec 1): 1 day active -> 100,000 * 0.04 * (1/365) = 10.96 SEK.
        // Remaining 30 days (Dec 2 - Dec 31) -> 100,000 * 0.04 * (30/365) = 328.77 SEK.
        // Year-end total: 10.96 + 328.77 = 339.73 SEK.
        const decimal openingBalance = 0m;
        const decimal interestRate = 0.04m;
        const int year = 2026;
        var transactions = new List<(decimal Amount, DateTime CreatedAt)>
        {
            (100000m, new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc))
        };
        var asOfDate = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var (accruedYtd, estimatedYearEnd) = AccountInterestCalculator.Calculate(
            openingBalance,
            transactions,
            year,
            interestRate,
            rateHistory: null,
            asOfDateUtc: asOfDate);

        // Assert:
        Assert.Equal(10.96m, accruedYtd);
        Assert.Equal(339.73m, estimatedYearEnd);
    }
}
