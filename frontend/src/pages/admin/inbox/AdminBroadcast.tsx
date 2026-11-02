import { useState, type FormEvent } from "react";
import { useCreateBroadcastMessage } from "../../../hooks/inbox/useStaffInbox";
import type { Categories } from "../../../types/inbox";
import { input, section } from "./adminStyles";
import AdminButton from "../../../components/admin/ui/AdminButton";
import AdminStatus from "../../../components/admin/ui/AdminStatus";
import { AdminSectionTitle } from "../../../components/admin/ui/AdminTitle";

// Same values customer form sends. Empty = no category.
const CATEGORIES: Categories[] = ["Sparkonto", "Sparmål", "Skatteunderlag", "Allmänt"];

export default function AdminBroadcast() {
    const broadcast = useCreateBroadcastMessage();
    const [subject, setSubject] = useState("");
    const [body, setBody] = useState("");
    const [category, setCategory] = useState<Categories | "">("");
    const [broadcastToAll, setBroadcastToAll] = useState(false);
    const [customerId, setCustomerId] = useState("");
    const [isInformationOnly, setIsInformationOnly] = useState(false);
    const [replyAllowed, setReplyAllowed] = useState(true);

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        // Sending to every customer cannot be undone; ask first.
        if (broadcastToAll && !window.confirm("Skicka till ALLA kunder?")) return;

        broadcast.mutate(
            {
                subject: subject.trim() || null,
                body: body.trim() || null,
                category: category || null,
                broadcastToAll,
                customerId: broadcastToAll ? null : Number(customerId),
                isInformationOnly,
                // Information-only messages never take replies.
                replyAllowed: isInformationOnly ? false : replyAllowed,
            },
            {
                onSuccess: () => {
                    setSubject("");
                    setBody("");
                    setCustomerId("");
                    setBroadcastToAll(false);
                },
            },
        );
    };

    return (
        <section className={section}>
            <AdminSectionTitle>Skicka meddelande</AdminSectionTitle>

            <form onSubmit={handleSubmit} className="flex flex-col gap-2 max-w-[600px]">
                <label className="flex items-center gap-2">
                    <input type="checkbox" checked={broadcastToAll} onChange={(e) => setBroadcastToAll(e.target.checked)} />
                    Skicka till alla kunder
                </label>
                <label className="flex flex-col">
                    Kund-ID
                    <input
                        className={input}
                        type="number"
                        min={1}
                        required={!broadcastToAll}
                        disabled={broadcastToAll}
                        value={customerId}
                        onChange={(e) => setCustomerId(e.target.value)}
                    />
                </label>
                <label className="flex flex-col">
                    Ämne
                    <input className={input} required value={subject} onChange={(e) => setSubject(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Kategori
                    <select className={input} value={category} onChange={(e) => setCategory(e.target.value as Categories | "")}>
                        <option value="">Ingen</option>
                        {CATEGORIES.map((name) => <option key={name} value={name}>{name}</option>)}
                    </select>
                </label>
                <label className="flex flex-col">
                    Meddelande
                    <textarea className={input} rows={5} required value={body} onChange={(e) => setBody(e.target.value)} />
                </label>
                <label className="flex items-center gap-2">
                    <input type="checkbox" checked={isInformationOnly} onChange={(e) => setIsInformationOnly(e.target.checked)} />
                    Endast information (visas som meddelanderuta, inget svar möjligt)
                </label>
                <label className="flex items-center gap-2">
                    <input
                        type="checkbox"
                        checked={isInformationOnly ? false : replyAllowed}
                        disabled={isInformationOnly}
                        onChange={(e) => setReplyAllowed(e.target.checked)}
                    />
                    Kunden får svara
                </label>
                <AdminButton type="submit" variant="green" className="self-start" disabled={broadcast.isPending}>
                    {broadcast.isPending ? "Skickar..." : "Skicka"}
                </AdminButton>
                {broadcast.isSuccess && <p role="status">Meddelande skickat.</p>}
                {broadcast.isError && <AdminStatus kind="error">Kunde inte skicka meddelande.</AdminStatus>}
            </form>
        </section>
    );
}
