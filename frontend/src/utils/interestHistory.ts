import type { Transaction } from "../services/transactionsService";
import { toDateKey } from "./date";

const round2 = (value: number) => Math.round(value * 100) / 100;
const MS_PER_DAY = 24 * 60 * 60 * 1000;

function startOfDay(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/** Lägger till (eller drar ifrån) månader. Dagen kläms till månadens sista dag, t.ex. 31 jan + 1 mån = 28/29 feb */
export function addMonths(date: Date, months: number): Date {
    const daysInMonth = new Date(date.getFullYear(), date.getMonth() + months + 1, 0).getDate();
    return new Date(date.getFullYear(), date.getMonth() + months, Math.min(date.getDate(), daysInMonth));
}

/** Antal dagar mellan idag och samma datum `months` månader bort (negativt = bakåt) */
export function daysForMonths(months: number, today = new Date()): number {
    const start = startOfDay(today);
    return Math.round(Math.abs(addMonths(start, months).getTime() - start.getTime()) / MS_PER_DAY);
}

/** Början av lokala dagen för (idag - days + 1), dvs. första dagen i en period på `days` dagar */
export function periodStart(days: number, today = new Date()): Date {
    return new Date(today.getFullYear(), today.getMonth(), today.getDate() - days + 1);
}

/**
 * Räknar fram intjänad ränta de senaste `days` dagarna. Samma modell som backend
 * (AccountInterestCalculator): enkel daglig ränta på dagens slutsaldo, saldo * ränta / 365.
 * Saldot per dag tas fram genom att gå bakåt från nuvarande saldo och backa varje dags transaktioner.
 * Använder nuvarande räntesats, så resultatet är en uppskattning om räntan ändrats under perioden.
 */
export function calculateEarnedInterest(
    balance: number,
    interestRate: number,
    createdAt: string,
    transactions: Transaction[],
    days: number,
    today = new Date(),
): number {
    const sumPerDay = new Map<string, number>();
    for (const tx of transactions) {
        if (tx.isPlanned) continue;
        const key = toDateKey(tx.createdAt);
        sumPerDay.set(key, (sumPerDay.get(key) ?? 0) + tx.amount);
    }

    const createdKey = toDateKey(createdAt);
    let endOfDayBalance = balance;
    let total = 0;

    for (let i = 0; i < days; i++) {
        const day = new Date(today.getFullYear(), today.getMonth(), today.getDate() - i);
        const key = toDateKey(day.toISOString());
        const isOpen = !createdKey || key >= createdKey;
        if (isOpen) total += Math.max(endOfDayBalance, 0) * interestRate / 365;
        endOfDayBalance -= sumPerDay.get(key) ?? 0;
    }

    return round2(total);
}

export type InterestForecast = {
    withoutInterest: number;
    withInterest: number;
    difference: number;
    totalDeposited: number;
    depositCount: number;
    endBalanceWith: number;
};

/** Insättningsdagar: idag, och vid månadsvis samma datum varje månad före periodens sista dag (12 mån = 12 insättningar) */
function depositDates(today: Date, lastDay: Date, monthly: boolean): Date[] {
    const dates: Date[] = [];
    for (let month = 0; ; month++) {
        const date = addMonths(today, month);
        if (date >= lastDay) break;
        dates.push(date);
        if (!monthly) break;
    }
    return dates;
}

/**
 * Prognos för de kommande `days` dagarna (från och med imorgon), med och utan en testinsättning.
 * Samma modell som calculateEarnedInterest: enkel daglig ränta på dagens slutsaldo, saldo * ränta / 365.
 */
export function buildInterestForecast(
    balance: number,
    interestRate: number,
    days: number,
    depositAmount: number,
    monthly: boolean,
    today = new Date(),
): InterestForecast {
    const start = startOfDay(today);
    const lastDay = new Date(start.getFullYear(), start.getMonth(), start.getDate() + days);
    const deposits = depositAmount > 0 ? depositDates(start, lastDay, monthly) : [];

    let balanceWith = balance;
    let nextDeposit = 0;
    let without = 0;
    let withDeposit = 0;

    for (let i = 0; i <= days; i++) {
        const day = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i);
        while (nextDeposit < deposits.length && deposits[nextDeposit] <= day) {
            balanceWith += depositAmount;
            nextDeposit++;
        }
        // Dagens ränta (i = 0) räknas redan som intjänad, prognosen börjar imorgon
        if (i === 0) continue;

        without += Math.max(balance, 0) * interestRate / 365;
        withDeposit += Math.max(balanceWith, 0) * interestRate / 365;
    }

    const totalDeposited = deposits.length * depositAmount;
    return {
        withoutInterest: round2(without),
        withInterest: round2(withDeposit),
        difference: round2(withDeposit - without),
        totalDeposited,
        depositCount: deposits.length,
        endBalanceWith: round2(balance + totalDeposited + withDeposit),
    };
}
