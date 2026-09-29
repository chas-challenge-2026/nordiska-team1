import { useEffect, useState } from "react";

export const CARD_IDS = ["accounts", "savings", "planned", "transactions"] as const;
export type CardId = (typeof CARD_IDS)[number];

const STORAGE_KEY = "overview-card-order";

function isCardId(value: unknown): value is CardId {
    return typeof value === "string" && (CARD_IDS as readonly string[]).includes(value);
}

/**
 * Never trust stored data. Keeps known ids only, removes duplicates,
 * appends ids missing from storage (e.g. cards added in later releases).
 */
function sanitize(value: unknown): CardId[] {
    if (!Array.isArray(value)) return [...CARD_IDS];
    const result = new Set<CardId>();
    for (const item of value) {
        if (isCardId(item)) result.add(item);
    }
    for (const id of CARD_IDS) result.add(id);
    return [...result];
}

function readOrder(): CardId[] {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        return raw ? sanitize(JSON.parse(raw)) : [...CARD_IDS];
    } catch {
        // Invalid JSON or storage unavailable (private mode, blocked).
        return [...CARD_IDS];
    }
}

export function useCardOrder() {
    const [order, setOrder] = useState<CardId[]>(readOrder);

    useEffect(() => {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(order));
        } catch {
            // Storage full or unavailable. Order still works for this session.
        }
    }, [order]);

    /** Moves card one step. Returns new index, or null if move not possible. */
    function move(id: CardId, delta: -1 | 1): number | null {
        const from = order.indexOf(id);
        const to = from + delta;
        if (from < 0 || to < 0 || to >= order.length) return null;

        const next = [...order];
        [next[from], next[to]] = [next[to], next[from]];
        setOrder(next);
        return to;
    }

    function reset() {
        setOrder([...CARD_IDS]);
    }

    return { order, setOrder, move, reset };
}
