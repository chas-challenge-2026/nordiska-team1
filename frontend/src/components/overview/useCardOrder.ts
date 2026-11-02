import { useState } from "react";
import { useUserStore } from "../../store/userStore";

export const CARD_IDS = ["accounts", "savings", "planned", "transactions", "inbox", "interest"] as const;
export type CardId = (typeof CARD_IDS)[number];

const FLAG_UNEXTENDED = "unextended";
const FLAG_ONLY_FAVS = "onlyFavs";

export type OverviewPreference = {
    showUnextended: boolean;
    showOnlyFavs: boolean;
    order: CardId[];
};

function isCardId(value: unknown): value is CardId {
    return typeof value === "string" && (CARD_IDS as readonly string[]).includes(value);
}

function sanitizeOrder(value: unknown[]): CardId[] {
    const result = new Set<CardId>();
    for (const item of value) if (isCardId(item)) result.add(item);
    for (const id of CARD_IDS) result.add(id);
    return [...result];
}

export function parsePreference(value: unknown): OverviewPreference {
    const arr = Array.isArray(value) ? value : [];
    return {
        showUnextended: arr.includes(FLAG_UNEXTENDED),
        showOnlyFavs: arr.includes(FLAG_ONLY_FAVS),
        order: sanitizeOrder(arr),
    };
}

export function serializePreference(pref: OverviewPreference): string[] {
    return [
        ...(pref.showUnextended ? [FLAG_UNEXTENDED] : []),
        ...(pref.showOnlyFavs ? [FLAG_ONLY_FAVS] : []),
        ...pref.order,
    ];
}

export function useCardOrder() {
    const user = useUserStore((state) => state.user);
    const [order, setOrder] = useState<CardId[]>(() => parsePreference(user?.overviewPreference).order);

    function beginEditing() {
        setOrder(parsePreference(user?.overviewPreference).order);
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