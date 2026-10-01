using System;
using System.Collections.Generic;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Reporting.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public class TaxReportInterestCalculationTests
{
    [Fact]
    public void CalculateAccruedInterest_WhenFullYearWithoutTransactions_ReturnsFullAnnualInterest()
    {
        // Arrange: 100,000 SEK all year (2026 = 365 days) at 4.0% (0.04)
        const decimal openingBalance = 100000m;
        const decimal interestRate = 0.04m;
        const int year = 2026;
        var transactions = Array.Empty<LedgerEntry>();

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, year, interestRate);

        // Assert: 100,000 * 0.04 = 4,000.00 SEK
        Assert.Equal(4000.00m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_WhenDepositOnDec31st_ReturnsOnlyOneDayInterest()
    {
        // Arrange: 0 opening balance, 100,000 SEK deposit on Dec 31st at 4.0% (0.04)
        const decimal openingBalance = 0m;
        const decimal interestRate = 0.04m;
        const int year = 2026; // 365 days
        var transactions = new List<LedgerEntry>
        {
            new()
            {
                Id = 1,
                AccountId = 1,
                Type = "deposit",
                Amount = 100000m,
                CreatedAt = new DateTime(2026, 12, 31, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, year, interestRate);

        // Assert: 100,000 * 0.04 * (1 / 365) = 10.9589... -> 10.96 SEK
        Assert.Equal(10.96m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_WhenDepositMidYear_ReturnsAccurateHalfYearInterest()
    {
        // Arrange: 0 opening balance, 100,000 SEK deposit on July 1st (2026: 184 days July 1 -> Dec 31) at 4.0%
        const decimal openingBalance = 0m;
        const decimal interestRate = 0.04m;
        const int year = 2026;
        var transactions = new List<LedgerEntry>
        {
            new()
            {
                Id = 1,
                AccountId = 1,
                Type = "deposit",
                Amount = 100000m,
                CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, year, interestRate);

        // Assert: 100,000 * 0.04 * (184 / 365) = 2016.438... -> 2016.44 SEK
        Assert.Equal(2016.44m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_WhenWithdrawalMidYear_ReturnsInterestOnlyForActiveDays()
    {
        // Arrange: 100,000 opening balance, withdrawn to 0 on July 1st (181 days Jan 1 -> June 30) at 4.0%
        const decimal openingBalance = 100000m;
        const decimal interestRate = 0.04m;
        const int year = 2026;
        var transactions = new List<LedgerEntry>
        {
            new()
            {
                Id = 1,
                AccountId = 1,
                Type = "withdrawal",
                Amount = -100000m,
                CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, year, interestRate);

        // Assert: 100,000 * 0.04 * (181 / 365) = 1983.561... -> 1983.56 SEK
        Assert.Equal(1983.56m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_InLeapYear_Uses366DaysDivisor()
    {
        // Arrange: 2024 is a leap year (366 days). 100,000 SEK deposit on Dec 31st at 3.66% (0.0366)
        const decimal openingBalance = 0m;
        const decimal interestRate = 0.0366m;
        const int leapYear = 2024;
        var transactions = new List<LedgerEntry>
        {
            new()
            {
                Id = 1,
                AccountId = 1,
                Type = "deposit",
                Amount = 100000m,
                CreatedAt = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, leapYear, interestRate);

        // Assert: 100,000 * 0.0366 * (1 / 366) = 10.00 SEK
        Assert.Equal(10.00m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_WhenMultipleTransactionsSameDay_CalculatesAccurately()
    {
        // Arrange: 0 opening balance.
        // Jan 1: deposit 10,000
        // Jan 1: deposit 20,000 (total balance 30,000 from day 1)
        const decimal openingBalance = 0m;
        const decimal interestRate = 0.05m;
        const int year = 2026;
        var transactions = new List<LedgerEntry>
        {
            new()
            {
                Id = 1,
                AccountId = 1,
                Type = "deposit",
                Amount = 10000m,
                CreatedAt = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = 2,
                AccountId = 1,
                Type = "deposit",
                Amount = 20000m,
                CreatedAt = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc)
            }
        };

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, year, interestRate);

        // Assert: 30,000 * 0.05 * (365 / 365) = 1500.00 SEK
        Assert.Equal(1500.00m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_WithRateChangeMidYear_CalculatesAccuratelyForBothPeriods()
    {
        // Arrange: 100,000 SEK opening balance all year (2026 = 365 days).
        // Rate period 1 (Jan 1 - Jun 30 = 181 days): 2.5% (0.025)
        // Rate period 2 (Jul 1 - Dec 31 = 184 days): 4.0% (0.040)
        const decimal openingBalance = 100000m;
        const decimal defaultRate = 0.04m;
        const int year = 2026;
        var transactions = Array.Empty<LedgerEntry>();

        var ratePeriods = new List<InterestRatePeriod>
        {
            new(0.025m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc)),
            new(0.040m, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), null)
        };

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, year, defaultRate, ratePeriods);

        // Assert:
        // Period 1: 100,000 * 0.025 * (181 / 365) = 1239.726...
        // Period 2: 100,000 * 0.040 * (184 / 365) = 2016.438...
        // Total: 3256.164... -> 3256.16 SEK
        Assert.Equal(3256.16m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_WithRateChangeAndTransactions_CalculatesAccurately()
    {
        // Arrange: 0 opening balance in 2026 (365 days).
        // Period 1: 2.0% (0.02) Jan 1 - Jun 30 (181 days)
        // Period 2: 5.0% (0.05) Jul 1 - Dec 31 (184 days)
        // Deposit Jan 1: 50,000 SEK
        // Deposit Jul 1: 50,000 SEK (total 100,000 SEK during period 2)
        const decimal openingBalance = 0m;
        const decimal defaultRate = 0.05m;
        const int year = 2026;
        var transactions = new List<LedgerEntry>
        {
            new()
            {
                Id = 1,
                AccountId = 1,
                Type = "deposit",
                Amount = 50000m,
                CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = 2,
                AccountId = 1,
                Type = "deposit",
                Amount = 50000m,
                CreatedAt = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        var ratePeriods = new List<InterestRatePeriod>
        {
            new(0.02m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc)),
            new(0.05m, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), null)
        };

        // Act
        var interest = ReportDataBuilder.CalculateAccruedInterest(openingBalance, transactions, year, defaultRate, ratePeriods);

        // Assert:
        // Period 1: 50,000 * 0.02 * (181 / 365) = 495.8904...
        // Period 2: 100,000 * 0.05 * (184 / 365) = 2520.5479...
        // Total: 3016.438... -> 3016.44 SEK
        Assert.Equal(3016.44m, interest);
    }
}
