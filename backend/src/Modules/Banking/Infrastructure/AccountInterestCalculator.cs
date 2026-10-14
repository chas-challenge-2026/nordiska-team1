using System;
using System.Collections.Generic;
using System.Linq;

namespace Nordiska.Modules.Banking.Infrastructure;

public static class AccountInterestCalculator
{
    public static (decimal AccruedYtd, decimal EstimatedYearEnd) Calculate(
        decimal openingBalance,
        IEnumerable<(decimal Amount, DateTime CreatedAt)> transactions,
        int year,
        decimal currentRate,
        IEnumerable<(decimal Rate, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc)>? rateHistory = null,
        DateTime? asOfDateUtc = null)
    {
        var now = asOfDateUtc ?? DateTime.UtcNow;
        var totalDaysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        var yearStartDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEndDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var today = now.Date < yearStartDate ? yearStartDate : (now.Date > yearEndDate.Date ? yearEndDate.Date : now.Date);
        var dayOfToday = (today - yearStartDate).Days;

        var yearTransactions = transactions
            .Where(t => t.CreatedAt >= yearStartDate && t.CreatedAt <= yearEndDate)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        var txByDay = yearTransactions
            .GroupBy(t => (t.CreatedAt.Date - yearStartDate.Date).Days)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var rates = rateHistory?.ToList();

        decimal accruedYtd = 0m;
        decimal currentBalance = openingBalance;

        for (int dayOffset = 0; dayOffset <= dayOfToday && dayOffset < totalDaysInYear; dayOffset++)
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

            decimal activeRate = currentRate;
            if (rates != null && rates.Count > 0)
            {
                var match = rates
                    .Where(p => p.EffectiveFromUtc.Date <= currentDayDate &&
                                (p.EffectiveToUtc == null || p.EffectiveToUtc.Value.Date >= currentDayDate))
                    .OrderByDescending(p => p.EffectiveFromUtc)
                    .FirstOrDefault();

                if (match != default)
                {
                    activeRate = match.Rate;
                }
            }

            if (activeRate > 0)
            {
                accruedYtd += currentBalance * activeRate / totalDaysInYear;
            }
        }

        var remainingDays = totalDaysInYear - (dayOfToday + 1);
        decimal remainingInterest = 0m;
        if (remainingDays > 0 && currentBalance > 0 && currentRate > 0)
        {
            remainingInterest = currentBalance * currentRate * remainingDays / totalDaysInYear;
        }

        return (
            Math.Round(accruedYtd, 2, MidpointRounding.AwayFromZero),
            Math.Round(accruedYtd + remainingInterest, 2, MidpointRounding.AwayFromZero)
        );
    }
}
