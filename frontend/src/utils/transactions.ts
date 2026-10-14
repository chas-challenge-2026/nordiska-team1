import type { Transaction } from "../services/transactionsService";

export type TransactionRow =
    | { kind: "single"; tx: Transaction }
    /** Båda benen i en överföring mellan egna konton, visas som en rad */
    | { kind: "transfer"; id: string; from: Transaction; to: Transaction };

export interface TransactionRowGroup {
    dateKey: string;
    /** createdAt för första raden i gruppen */
    date: string;
    rows: TransactionRow[];
}

function isOtherLeg(tx: Transaction, other: Transaction): boolean {
    if (other.id === tx.id || other.type !== "transfer") return false;
    if (tx.correlationId) return other.correlationId === tx.correlationId;
    // Vissa överföringar (t.ex. insättningar till sparmål) saknar correlationId
    return other.createdAt === tx.createdAt
        && other.accountId === tx.targetAccountId
        && other.targetAccountId === tx.accountId
        && other.amount === -tx.amount;
}

/**
 * Slår ihop överföringar vars båda ben finns i listan till en rad.
 * Saknas det andra benet (t.ex. när man filtrerat på ett konto) visas benet som vanligt.
 */
export function mergeTransferPairs(items: Transaction[]): TransactionRow[] {
    const rows: TransactionRow[] = [];
    const merged = new Set<number>();

    for (const tx of items) {
        if (merged.has(tx.id)) continue;

        const other = tx.type === "transfer"
            ? items.find(o => !merged.has(o.id) && isOtherLeg(tx, o))
            : undefined;

        if (!other) {
            rows.push({ kind: "single", tx });
            continue;
        }

        const [from, to] = tx.amount < 0 ? [tx, other] : [other, tx];
        rows.push({ kind: "transfer", id: `transfer-${from.id}-${to.id}`, from, to });
        merged.add(tx.id);
        merged.add(other.id);
    }

    return rows;
}

export function rowDate(row: TransactionRow): string {
    return row.kind === "transfer" ? row.from.createdAt : row.tx.createdAt;
}

function localDateKey(iso: string): string {
    const d = new Date(iso);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

/** Grupperar rader per lokalt datum med bibehållen ordning */
export function groupRowsByDate(rows: TransactionRow[]): TransactionRowGroup[] {
    const groups: TransactionRowGroup[] = [];

    for (const row of rows) {
        const date = rowDate(row);
        const dateKey = localDateKey(date);
        const last = groups[groups.length - 1];

        if (last?.dateKey === dateKey) {
            last.rows.push(row);
        } else {
            groups.push({ dateKey, date, rows: [row] });
        }
    }

    return groups;
}
