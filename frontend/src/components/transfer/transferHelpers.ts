import type { TransferAccount } from "../../constants/transferAccounts";

// Ingen backend än — simulerar överföringen med en fördröjning. Skriv "fail"
// i notisfältet för att medvetet trigga ett misslyckande under test. Byt ut
// mot ett riktigt API-anrop här den dagen backend finns.
export const SIMULATED_TRANSFER_DELAY_MS = 2500;

export function shouldSimulateFailure(transferName: string) {
    return transferName.trim().toLowerCase().includes("fail");
}

export function formatSek(amount: number) {
    return amount.toLocaleString("sv-SE", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    });
}

export function parseAmount(raw: string) {
    const parsed = parseFloat(raw.replace(/\s/g, "").replace(",", "."));
    return Number.isFinite(parsed) ? parsed : 0;
}

export function todayIso() {
    return new Date().toISOString().slice(0, 10);
}

export function matchesSearch(account: TransferAccount, query: string) {
    if (!query) return true;
    return `${account.name} ${account.meta}`.toLowerCase().includes(query);
}
