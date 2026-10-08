import { useState } from "react";
import { updateCustomerOverviewLayout } from "../../services/customerService";
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
    const updateUser = useUserStore((state) => state.updateUser);

    const [order, setOrderState] = useState<CardId[]>(() => sanitize(user?.overviewPreference));

    async function saveOrder(next: CardId[]) {
        setOrderState(next);

        if (!user?.id) return;

        try {
            await updateCustomerOverviewLayout(user.id, next);
            updateUser({overviewPreference: next,});
        } catch (error) {
            console.error("Could not save overview layout", error);
        }
    }

    function move(id: CardId, delta: -1 | 1): number | null {
        const from = order.indexOf(id);
        const to = from + delta;
        if (from < 0 || to < 0 || to >= order.length) return null;

        const next = [...order];
        [next[from], next[to]] = [next[to], next[from]];

        saveOrder(next);

        return to;
    }

    function reset() {
        saveOrder([...CARD_IDS]);
    }

    return {
        order,
        setOrder: saveOrder,
        move,
        reset,
    };
}