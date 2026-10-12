import { useState } from "react";
import { useUserStore } from "../../store/userStore";

export const CARD_IDS = ["accounts", "savings", "planned", "transactions"] as const;
export type CardId = (typeof CARD_IDS)[number];

function isCardId(value: unknown): value is CardId {
    return typeof value === "string" && (CARD_IDS as readonly string[]).includes(value);
}

function sanitize(value: unknown): CardId[] {
    if (!Array.isArray(value)) return [...CARD_IDS];
    const result = new Set<CardId>();
    for (const item of value) {
        if (isCardId(item)) result.add(item);
    }
    for (const id of CARD_IDS) result.add(id);
    return [...result];
}

export function useCardOrder() {
    const user = useUserStore((state) => state.user);
    const [order, setOrder] = useState<CardId[]>(() => sanitize(user?.overviewPreference));

    function beginEditing() {
        setOrder(sanitize(user?.overviewPreference));
    }

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

    return {
        order,
        setOrder,
        beginEditing,
        move,
        reset,
    };
}