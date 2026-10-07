import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import {
    useCloseThread,
    useCreateBroadcastMessage,
    usePublishNewTerms,
    useReopenThread,
    useStaffSendReply,
    useStaffThreads,
    useSupportThread,
} from "../../../hooks/useInbox";
import { itemsOf, pagingOf } from "../../../components/inbox/inboxAdapters";
import { formatDate, formatTime } from "../../../utils/date";

const btn = "border py-1 px-2 cursor-pointer hover:bg-light-gray font-semibold disabled:cursor-not-allowed disabled:opacity-50";
const input = "border p-1";
const section = "border p-4 flex flex-col gap-3";

// Same values customer form sends. Empty = no category.
const CATEGORIES = ["Sparkonto", "Sparmål", "Skatteunderlag", "Allmänt"];

function text(value: unknown): string {
    return typeof value === "string" || typeof value === "number" ? String(value) : "";
}

export default function AdminInbox() {
    const navigate = useNavigate();

    return (
        <main className="font-inter p-5 flex flex-col gap-8">
            <header className="flex justify-between items-center border-b-2 pb-3">
                <h1 className="text-2xl">Kommunikationscenter</h1>
                <button type="button" onClick={() => navigate("/admin")} className={btn}>
                    Tillbaka
                </button>
            </header>

            <ThreadsSection />
            <BroadcastSection />
            <PublishTermsSection />
        </main>
    );
}

/* ----------------------------- TRÅDAR ----------------------------- */

function ThreadsSection() {
    const [page, setPage] = useState(1);
    const [draft, setDraft] = useState({ customerId: "", status: "", searchTerm: "" });
    const [filters, setFilters] = useState(draft);
    const [selectedId, setSelectedId] = useState<number>();

    const threads = useStaffThreads(
        page,
        10,
        filters.customerId ? Number(filters.customerId) : undefined,
        filters.status || undefined,
        filters.searchTerm || undefined,
    );
    const closeThread = useCloseThread();
    const reopenThread = useReopenThread();

    const rows = itemsOf(threads.data);
    const paging = pagingOf(threads.data);

    const handleFilter = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        setPage(1);
        setFilters(draft);
    };

    return (
        <section className={section}>
            <h2 className="text-xl font-semibold">Trådar</h2>

            <form onSubmit={handleFilter} className="flex flex-wrap items-end gap-3">
                <label className="flex flex-col">
                    Sök
                    <input
                        className={input}
                        value={draft.searchTerm}
                        onChange={(e) => setDraft({ ...draft, searchTerm: e.target.value })}
                    />
                </label>
                <label className="flex flex-col">
                    Status
                    <input
                        className={input}
                        value={draft.status}
                        onChange={(e) => setDraft({ ...draft, status: e.target.value })}
                    />
                </label>
                <label className="flex flex-col">
                    Kund-ID
                    <input
                        className={input}
                        type="number"
                        min={1}
                        value={draft.customerId}
                        onChange={(e) => setDraft({ ...draft, customerId: e.target.value })}
                    />
                </label>
                <button type="submit" className={btn}>Filtrera</button>
            </form>

            {threads.isPending && <p role="status">Laddar...</p>}
            {threads.isError && <p role="alert" className="text-error">Kunde inte hämta trådar.</p>}
            {(closeThread.isError || reopenThread.isError) && (
                <p role="alert" className="text-error">Kunde inte ändra status.</p>
            )}
            {threads.isSuccess && rows.length === 0 && <p>Inga trådar.</p>}

            {rows.length > 0 && (
                <div className="overflow-x-auto">
                    <table className="w-full text-left">
                        <thead>
                            <tr className="border-b">
                                <th className="p-1">ID</th>
                                <th className="p-1">Kund</th>
                                <th className="p-1">Ämne</th>
                                <th className="p-1">Status</th>
                                <th className="p-1">Senast</th>
                                <th className="p-1"></th>
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map((raw) => {
                                const id = typeof raw.id === "number" ? raw.id : null;
                                if (id === null) return null;
                                const status = text(raw.status);
                                const isClosed = status.toLowerCase() === "closed";
                                const busy = closeThread.isPending || reopenThread.isPending;
                                return (
                                    <tr key={id} className={`border-b ${id === selectedId ? "bg-light-gray" : ""}`}>
                                        <td className="p-1">{id}</td>
                                        <td className="p-1">{text(raw.customerId)}</td>
                                        <td className="p-1">{text(raw.subject) || "(inget ämne)"}</td>
                                        <td className="p-1">{status}</td>
                                        <td className="p-1">{formatDate(text(raw.lastMessageAt))}</td>
                                        <td className="p-1 flex gap-2">
                                            <button type="button" className={btn} onClick={() => setSelectedId(id)}>
                                                Öppna
                                            </button>
                                            <button
                                                type="button"
                                                className={btn}
                                                disabled={busy}
                                                onClick={() => (isClosed ? reopenThread.mutate(id) : closeThread.mutate(id))}
                                            >
                                                {isClosed ? "Återöppna" : "Stäng"}
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}

            {paging && paging.totalPages > 1 && (
                <div className="flex items-center gap-3">
                    <button type="button" className={btn} disabled={!paging.hasPreviousPage} onClick={() => setPage(paging.page - 1)}>
                        Föregående
                    </button>
                    <span>Sida {paging.page} av {paging.totalPages}</span>
                    <button type="button" className={btn} disabled={!paging.hasNextPage} onClick={() => setPage(paging.page + 1)}>
                        Nästa
                    </button>
                </div>
            )}

            {selectedId !== undefined && <ThreadPanel key={selectedId} id={selectedId} />}
        </section>
    );
}

/* ----------------------------- VALD TRÅD ----------------------------- */

function ThreadPanel({ id }: { id: number }) {
    const thread = useSupportThread(id);
    const sendReply = useStaffSendReply();
    const [body, setBody] = useState("");
    const [replyAllowed, setReplyAllowed] = useState(true);

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const trimmed = body.trim();
        if (!trimmed) return;
        sendReply.mutate({ id, body: trimmed, replyAllowed }, { onSuccess: () => setBody("") });
    };

    return (
        <div className="border-t-2 pt-3 flex flex-col gap-3">
            <h3 className="text-lg font-semibold">Tråd {id}</h3>

            {thread.isPending && <p role="status">Laddar...</p>}
            {thread.isError && <p role="alert" className="text-error">Kunde inte hämta tråden.</p>}

            {thread.data && (
                <>
                    <p>
                        <strong>Ämne:</strong> {thread.data.subject ?? "(inget ämne)"} |{" "}
                        <strong>Status:</strong> {thread.data.status ?? "-"}
                    </p>
                    <ul className="flex flex-col gap-2">
                        {thread.data.messages.map((message) => (
                            <li key={message.id} className="border p-2">
                                <p className="text-sm">
                                    {message.senderName ?? message.senderType ?? "Okänd"} ({formatDate(message.sentAt)} {formatTime(message.sentAt)})
                                </p>
                                <p className="whitespace-pre-wrap break-words">{message.body}</p>
                            </li>
                        ))}
                    </ul>
                </>
            )}

            <form onSubmit={handleSubmit} className="flex flex-col gap-2">
                <label className="flex flex-col">
                    Svar
                    <textarea
                        className={input}
                        rows={4}
                        required
                        value={body}
                        onChange={(e) => setBody(e.target.value)}
                    />
                </label>
                <label className="flex items-center gap-2">
                    <input type="checkbox" checked={replyAllowed} onChange={(e) => setReplyAllowed(e.target.checked)} />
                    Kunden får svara
                </label>
                <button type="submit" className={`${btn} self-start`} disabled={sendReply.isPending}>
                    {sendReply.isPending ? "Skickar..." : "Skicka svar"}
                </button>
                {sendReply.isSuccess && <p role="status">Svar skickat.</p>}
                {sendReply.isError && <p role="alert" className="text-error">Kunde inte skicka svar.</p>}
            </form>
        </div>
    );
}

/* ----------------------------- SKICKA MEDDELANDE ----------------------------- */

function BroadcastSection() {
    const broadcast = useCreateBroadcastMessage();
    const [subject, setSubject] = useState("");
    const [body, setBody] = useState("");
    const [category, setCategory] = useState("");
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
                replyAllowed,
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
            <h2 className="text-xl font-semibold">Skicka meddelande</h2>

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
                    <select className={input} value={category} onChange={(e) => setCategory(e.target.value)}>
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
                    Endast information
                </label>
                <label className="flex items-center gap-2">
                    <input type="checkbox" checked={replyAllowed} onChange={(e) => setReplyAllowed(e.target.checked)} />
                    Kunden får svara
                </label>
                <button type="submit" className={`${btn} self-start`} disabled={broadcast.isPending}>
                    {broadcast.isPending ? "Skickar..." : "Skicka"}
                </button>
                {broadcast.isSuccess && <p role="status">Meddelande skickat.</p>}
                {broadcast.isError && <p role="alert" className="text-error">Kunde inte skicka meddelande.</p>}
            </form>
        </section>
    );
}

/* ----------------------------- PUBLICERA VILLKOR ----------------------------- */

function PublishTermsSection() {
    const publish = usePublishNewTerms();
    const [code, setCode] = useState("");
    const [title, setTitle] = useState("");
    const [version, setVersion] = useState("");
    const [documentId, setDocumentId] = useState("");
    const [effectiveFrom, setEffectiveFrom] = useState("");

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        // Publishing terms reaches customers; ask first.
        if (!window.confirm(`Publicera "${title}" version ${version}?`)) return;

        publish.mutate(
            {
                code: code.trim(),
                title: title.trim(),
                version: Number(version),
                documentId: Number(documentId),
                effectiveFrom: new Date(effectiveFrom).toISOString(),
            },
            {
                onSuccess: () => {
                    setCode("");
                    setTitle("");
                    setVersion("");
                    setDocumentId("");
                    setEffectiveFrom("");
                },
            },
        );
    };

    return (
        <section className={section}>
            <h2 className="text-xl font-semibold">Publicera villkor</h2>

            <form onSubmit={handleSubmit} className="flex flex-col gap-2 max-w-[600px]">
                <label className="flex flex-col">
                    Kod
                    <input className={input} required value={code} onChange={(e) => setCode(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Titel
                    <input className={input} required value={title} onChange={(e) => setTitle(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Version
                    <input className={input} type="number" min={1} required value={version} onChange={(e) => setVersion(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Dokument-ID
                    <input className={input} type="number" min={1} required value={documentId} onChange={(e) => setDocumentId(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Gäller från
                    <input className={input} type="date" required value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} />
                </label>
                <button type="submit" className={`${btn} self-start`} disabled={publish.isPending}>
                    {publish.isPending ? "Publicerar..." : "Publicera"}
                </button>
                {publish.isSuccess && <p role="status">Villkor publicerade.</p>}
                {publish.isError && <p role="alert" className="text-error">Kunde inte publicera villkor.</p>}
            </form>
        </section>
    );
}
