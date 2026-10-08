import { useEffect, useState } from "react";
import type { Payee } from "../constants/transferAccounts";

// Externa mottagare saknar backend-stöd — de som användaren lägger till
// sparas som mockad data i localStorage så att de finns kvar vid omladdning.
const STORAGE_KEY = "transfer-external-accounts";

function isPayee(value: unknown): value is Payee {
    if (typeof value !== "object" || value === null) return false;
    const p = value as Record<string, unknown>;
    return (
        typeof p.id === "string" &&
        p.own === false &&
        (p.kind === "bg" || p.kind === "bank") &&
        typeof p.name === "string" &&
        typeof p.meta === "string"
    );
}

/** Never trust stored data. Keeps valid payees only. */
function readAccounts(): Payee[] {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        const parsed: unknown = raw ? JSON.parse(raw) : [];
        return Array.isArray(parsed) ? parsed.filter(isPayee) : [];
    } catch {
        // Invalid JSON or storage unavailable (private mode, blocked).
        return [];
    }
}

export function useExternalAccounts() {
    const [accounts, setAccounts] = useState<Payee[]>(readAccounts);

    useEffect(() => {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(accounts));
        } catch {
            // Storage full or unavailable. Accounts still work for this session.
        }
    }, [accounts]);

    function addAccount(account: Omit<Payee, "id" | "own">): Payee {
        const payee: Payee = {
            id: `custom-${crypto.randomUUID()}`,
            own: false,
            ...account,
        };
        setAccounts((prev) => [...prev, payee]);
        return payee;
    }

    return { accounts, addAccount };
}
