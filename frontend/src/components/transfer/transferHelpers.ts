import type { OwnAccount, TransferAccount } from "../../constants/transferAccounts";
import type { Account } from "../../services/accountsService";
import type { TFunction } from "i18next";

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

// Backend lagrar plannedDate som en Postgres timestamptz, vilket kräver ett
// fullt UTC-datum (Kind=Utc) — ett bart "YYYY-MM-DD"-datum ger ett 500-fel.
export function toPlannedDateIso(dateStr: string) {
    return new Date(`${dateStr}T00:00:00Z`).toISOString();
}

// Backend kör bara "week" | "month" | "year" som återkommande. Andra värden
// körs en gång och tas sedan bort.
export type RepeatInterval = "week" | "month" | "year";

export function isRepeatInterval(repeating: string | undefined): repeating is RepeatInterval {
    return repeating === "week" || repeating === "month" || repeating === "year";
}

/** Fyller i redigeringsformuläret från ett befintligt repeating-värde. */
export function fromRepeating(repeating: string | undefined) {
    if (isRepeatInterval(repeating)) {
        return { recurring: true, interval: repeating };
    }
    return { recurring: false, interval: "month" as RepeatInterval };
}

export function addRepeatIso(dateStr: string, repeating: RepeatInterval) {
    const d = new Date(`${dateStr}T00:00:00Z`);
    if (repeating === "week") d.setUTCDate(d.getUTCDate() + 7);
    else if (repeating === "month") d.setUTCMonth(d.getUTCMonth() + 1);
    else d.setUTCFullYear(d.getUTCFullYear() + 1);
    return d.toISOString().slice(0, 10);
}

/** Nästa bankdag efter dateStr — lördag och söndag hoppas över. */
export function nextBankDayIso(dateStr: string) {
    const d = new Date(`${dateStr}T00:00:00Z`);
    do {
        d.setUTCDate(d.getUTCDate() + 1);
    } while (d.getUTCDay() === 0 || d.getUTCDay() === 6);
    return d.toISOString().slice(0, 10);
}

/** Text för ett repeating-värde, t.ex. "Månadsvis". Tom sträng om det inte upprepas. */
export function repeatingLabel(repeating: string, t: TFunction) {
    return isRepeatInterval(repeating)
        ? t(`page-transfer.repeating.${repeating}`)
        : "";
}

export function matchesSearch(account: TransferAccount, query: string) {
    if (!query) return true;
    return `${account.name} ${account.meta}`.toLowerCase().includes(query);
}

export function toOwnAccount(account: Account): OwnAccount {
    return {
        id: String(account.id),
        own: true,
        type: account.accountType,
        number: account.accountNumber,
        name: account.accountName?.trim() || account.accountType,
        meta: account.accountNumber,
        balance: account.balance,
    };
}
