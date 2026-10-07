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

export type RepeatInterval = "week" | "month" | "year" | "custom";

export const CUSTOM_DAYS_MAX = 365;

/** Antal dagar för eget intervall, eller null om värdet inte är 1–365. */
export function parseCustomDays(raw: string) {
    const trimmed = raw.trim();
    if (!/^\d+$/.test(trimmed)) return null;
    const days = Number(trimmed);
    return days >= 1 && days <= CUSTOM_DAYS_MAX ? days : null;
}

// Backend stödjer "week" | "month" | "year". Eget intervall skickas som
// "days:N" — det har inget backend-stöd än och körs då bara en gång.
export function toRepeating(interval: RepeatInterval, customDays: number | null) {
    if (interval !== "custom") return interval;
    return customDays === null ? undefined : `days:${customDays}`;
}

export function parseRepeatingDays(repeating: string) {
    const match = /^days:(\d+)$/.exec(repeating);
    return match ? Number(match[1]) : null;
}

export function addRepeatIso(dateStr: string, repeating: string) {
    const d = new Date(`${dateStr}T00:00:00Z`);
    const days = parseRepeatingDays(repeating);
    if (repeating === "week") d.setUTCDate(d.getUTCDate() + 7);
    else if (repeating === "month") d.setUTCMonth(d.getUTCMonth() + 1);
    else if (repeating === "year") d.setUTCFullYear(d.getUTCFullYear() + 1);
    else if (days !== null) d.setUTCDate(d.getUTCDate() + days);
    return d.toISOString().slice(0, 10);
}

/** Text för ett repeating-värde, t.ex. "Månadsvis" eller "Med 14 dagars intervall". */
export function repeatingLabel(repeating: string, t: TFunction) {
    if (repeating === "week" || repeating === "month" || repeating === "year") {
        return t(`page-transfer.repeating.${repeating}`);
    }
    const days = parseRepeatingDays(repeating);
    return days !== null
        ? t("page-transfer.repeating.days", { count: days })
        : repeating;
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
