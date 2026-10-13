import { useEffect, useState } from "react";
import { todayIso } from "../components/transfer/transferHelpers";

// Externa betalningar går inte fram direkt. De som skickats idag sparas som
// mockad data i localStorage och visas som "kommande" tills de är framme.
const STORAGE_KEY = "transfer-pending-transfers";

export type PendingTransfer = {
    id: string;
    name: string;
    fromName: string;
    toName: string;
    sum: number;
    /** YYYY-MM-DD, dagen betalningen beräknas vara framme. */
    arrivesAt: string;
};

function isPendingTransfer(value: unknown): value is PendingTransfer {
    if (typeof value !== "object" || value === null) return false;
    const p = value as Record<string, unknown>;
    return (
        typeof p.id === "string" &&
        typeof p.name === "string" &&
        typeof p.fromName === "string" &&
        typeof p.toName === "string" &&
        typeof p.sum === "number" &&
        typeof p.arrivesAt === "string"
    );
}

/** Never trust stored data. Keeps valid entries only. */
function readTransfers(): PendingTransfer[] {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        const parsed: unknown = raw ? JSON.parse(raw) : [];
        return Array.isArray(parsed) ? parsed.filter(isPendingTransfer) : [];
    } catch {
        // Invalid JSON or storage unavailable (private mode, blocked).
        return [];
    }
}

export function usePendingTransfers() {
    const [transfers, setTransfers] = useState<PendingTransfer[]>(readTransfers);

    useEffect(() => {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(transfers));
        } catch {
            // Storage full or unavailable. Transfers still show for this session.
        }
    }, [transfers]);

    function addPending(transfer: Omit<PendingTransfer, "id">) {
        setTransfers((prev) => [
            ...prev,
            { id: `pending-${crypto.randomUUID()}`, ...transfer },
        ]);
    }

    // Framme-datum som passerat = betalningen är genomförd och visas inte längre.
    const today = todayIso();
    const pendingTransfers = transfers
        .filter((p) => p.arrivesAt >= today)
        .sort((a, b) => a.arrivesAt.localeCompare(b.arrivesAt));

    return { pendingTransfers, addPending };
}
