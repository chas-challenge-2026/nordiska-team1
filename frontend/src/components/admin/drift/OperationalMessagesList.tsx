
import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    deleteOperationalMessage,
    getOperationalMessage,
} from "../../../services/adminService";
import { useServiceAlertStore } from "../../../store/serviceAlertStore";
import AdminButton from "../ui/AdminButton";
import AdminStatus from "../ui/AdminStatus";
import { AdminPageTitle } from "../ui/AdminTitle";
import OperationalMessageForm, {
    type OperationalMessageData,
} from "./OperationalMessageForm";

function isWithinDateRange(item: OperationalMessageData) {
    const now = Date.now();
    const start = item.startDate
        ? Date.parse(item.startDate)
        : -Infinity;
    const end = item.endDate
        ? Date.parse(item.endDate)
        : Infinity;

    return now >= start && now <= end;
}

export default function OperationalMessagesList() {
    const { i18n } = useTranslation();
    const language = i18n.language.startsWith("sv") ? "sv" : "en";

    const [messages, setMessages] = useState<OperationalMessageData[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [deletingId, setDeletingId] = useState<number | null>(null);
    const [editingItem, setEditingItem] =
        useState<OperationalMessageData | null>(null);

    const loadMessages = useCallback(async () => {
        try {
            const data = await getOperationalMessage();

            const result: OperationalMessageData[] = Array.isArray(data)
                ? data
                : data
                ? [data]
                : [];

            setMessages([...result].sort((a, b) => a.priority - b.priority));
            setError("");
        } catch (err) {
            console.error(err);
            setError("Kunde inte hämta driftmeddelanden.");
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        let cancelled = false;

        async function fetchMessages() {
            try {
                const data = await getOperationalMessage();

                if (cancelled) return;

                const result: OperationalMessageData[] = Array.isArray(data)
                    ? data
                    : data
                    ? [data]
                    : [];

                setMessages([...result].sort((a, b) => a.priority - b.priority));
                setError("");
            } catch (err) {
                if (!cancelled) {
                    console.error(err);
                    setError("Kunde inte hämta driftmeddelanden.");
                }
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        }

        void fetchMessages();

        return () => {
            cancelled = true;
        };
    }, []);

    async function handleDelete(id: number) {
        const confirmed = window.confirm(
            "Vill du ta bort det här driftmeddelandet?",
        );

        if (!confirmed) return;

        setDeletingId(id);
        setError("");

        try {
            await deleteOperationalMessage(id);

            setMessages((previous) =>
                previous.filter((message) => message.id !== id),
            );

            if (editingItem?.id === id) {
                setEditingItem(null);
            }

            const store = useServiceAlertStore.getState();

            if (store.alert?.id === String(id)) {
                store.closeAlert();
            }
        } catch (err) {
            console.error(err);
            setError("Kunde inte ta bort driftmeddelandet.");
        } finally {
            setDeletingId(null);
        }
    }

    return (
        <section className="space-y-6">
            {error && <AdminStatus kind="error">{error}</AdminStatus>}

            {editingItem && (
                <>
                <AdminPageTitle>Redigera driftmeddelande</AdminPageTitle>
                <OperationalMessageForm
                    key={editingItem.id}
                    item={editingItem}
                    onCancel={() => setEditingItem(null)}
                    onSaved={async () => {
                        setEditingItem(null);
                        await loadMessages();
                    }}
                />
                </>
            )}

            {!editingItem && (
                <>
                <AdminPageTitle>Aktuella driftmeddelanden</AdminPageTitle>

                {loading
                ? ( <AdminStatus kind="loading">Laddar driftmeddelanden...</AdminStatus>) : messages.length === 0 ? (
                    <AdminStatus kind="empty">Inga driftmeddelanden hittades.</AdminStatus>)
                : (
                    <div className="space-y-3">

                        {messages.map((item) => {const currentlyActive = item.isActive && isWithinDateRange(item);

                            return (
                                <article key={item.id} className="space-y-3 rounded-lg border p-4">
                                    <div className="flex flex-wrap items-center justify-between gap-2">
                                        <h3 className="font-semibold"> {item.title?.[language] ||`Driftmeddelande #${item.id}`}</h3>

                                        <span className={`py-3 px-4 text-sm font-semibold rounded-md ${currentlyActive ? "bg-success" : "bg-gray-100"}`}>

                                            {currentlyActive
                                                ? "Aktivt"
                                                : item.isActive
                                                    ? "Utanför giltighetsperiod"
                                                    : "Inaktivt"}
                                        </span>
                                    </div>

                                    <p> {item.message?.[language] || "Ingen meddelandetext"}</p>

                                    <div className="space-y-1 text-sm text-gray-500">
                                        <p>Typ: {item.severity ?? "Ej angiven"}</p>
                                        <p>Prioritet: {item.priority}</p>
                                        <p>
                                            Start:{" "}
                                            {item.startDate
                                                ? new Date(
                                                    item.startDate,
                                                ).toLocaleString()
                                                : "Ingen starttid"}
                                        </p>
                                        <p>
                                            Slut:{" "}
                                            {item.endDate
                                                ? new Date(
                                                    item.endDate,
                                                ).toLocaleString()
                                                : "Ingen sluttid"}
                                        </p>
                                    </div>

                                    <div className="flex justify-end gap-2">
                                        <AdminButton variant="yellow" onClick={() => setEditingItem(item)}>
                                            Redigera
                                        </AdminButton>

                                        <AdminButton
                                            variant="red"
                                            disabled={deletingId === item.id}
                                            onClick={() => void handleDelete(item.id)}
                                        >
                                            {deletingId === item.id
                                                ? "Tar bort..."
                                                : "Ta bort"}
                                        </AdminButton>
                                    </div>
                                </article>
                            );
                        })}
                    </div>
                )}
            </>

            )}
        </section>

    );
}