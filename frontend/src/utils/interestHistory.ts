import type { Transaction } from "../services/transactionsService";
import { toDateKey } from "./date";

export type InterestHistoryPoint = {
    /** Lokalt datum som YYYY-MM-DD */
    date: string;
    /** Ackumulerad ränta från periodens start till och med dagen */
    interest: number;
};

export type InterestHistory = {
    totalInterest: number;
    points: InterestHistoryPoint[];
};

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
export function buildInterestHistory(
    balance: number,
    interestRate: number,
    createdAt: string,
    transactions: Transaction[],
    days: number,
    today = new Date(),
): InterestHistory {
    const sumPerDay = new Map<string, number>();
    for (const tx of transactions) {
        if (tx.isPlanned) continue;
        const key = toDateKey(tx.createdAt);
        sumPerDay.set(key, (sumPerDay.get(key) ?? 0) + tx.amount);
    }

    const createdKey = toDateKey(createdAt);
    const dailyInterest: { date: string; interest: number }[] = [];
    let endOfDayBalance = balance;

    for (let i = 0; i < days; i++) {
        const day = new Date(today.getFullYear(), today.getMonth(), today.getDate() - i);
        const key = toDateKey(day.toISOString());
        const isOpen = !createdKey || key >= createdKey;
        const interest = isOpen ? Math.max(endOfDayBalance, 0) * interestRate / 365 : 0;

        dailyInterest.push({ date: key, interest });
        endOfDayBalance -= sumPerDay.get(key) ?? 0;
    }

    let accumulated = 0;
    const points = dailyInterest.reverse().map(({ date, interest }) => {
        accumulated += interest;
        return { date, interest: Math.round(accumulated * 100) / 100 };
    });

    return { totalInterest: Math.round(accumulated * 100) / 100, points };
}
