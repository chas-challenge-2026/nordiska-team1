const round2 = (value: number) => Math.round(value * 100) / 100;

/** Lägger till månader. Dagen kläms till månadens sista dag, t.ex. 31 jan + 1 mån = 28/29 feb */
export function addMonths(date: Date, months: number): Date {
    const daysInMonth = new Date(date.getFullYear(), date.getMonth() + months + 1, 0).getDate();
    return new Date(date.getFullYear(), date.getMonth() + months, Math.min(date.getDate(), daysInMonth));
}

export type SavingsForecast = {
    endBalance: number;
    totalInterest: number;
    totalDeposited: number;
};

/**
 * Sparprognos för `years` år framåt med ett månadssparande.
 * Samma modell som backend (AccountInterestCalculator): enkel daglig ränta på dagens slutsaldo, saldo * ränta / 365.
 * Räntan betalas ut till kontot vid varje månadsskifte och ger därefter ränta på räntan.
 * Månadssparandet sätts in idag och sedan samma datum varje månad (1 år = 12 insättningar).
 */
export function buildSavingsForecast(
    balance: number,
    interestRate: number,
    monthlyAmount: number,
    years: number,
    today = new Date(),
): SavingsForecast {
    const start = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const end = addMonths(start, 12 * years);

    let currentBalance = balance;
    let totalDeposited = 0;
    let paidInterest = 0;
    let accrued = 0;
    let depositIndex = 0;
    let nextDeposit = start;

    for (let day = start; day <= end; day = new Date(day.getFullYear(), day.getMonth(), day.getDate() + 1)) {
        while (monthlyAmount > 0 && nextDeposit <= day && nextDeposit < end) {
            currentBalance += monthlyAmount;
            totalDeposited += monthlyAmount;
            nextDeposit = addMonths(start, ++depositIndex);
        }
        // Dagens ränta räknas redan in i kontots intjänade ränta, prognosen börjar imorgon
        if (day.getTime() === start.getTime()) continue;

        accrued += Math.max(currentBalance, 0) * interestRate / 365;

        // Ränteutbetalning vid månadsskiftet
        const isLastDayOfMonth = new Date(day.getFullYear(), day.getMonth(), day.getDate() + 1).getDate() === 1;
        if (isLastDayOfMonth) {
            currentBalance += accrued;
            paidInterest += accrued;
            accrued = 0;
        }
    }

    const totalInterest = paidInterest + accrued;
    return {
        endBalance: round2(balance + totalDeposited + totalInterest),
        totalInterest: round2(totalInterest),
        totalDeposited: round2(totalDeposited),
    };
}
