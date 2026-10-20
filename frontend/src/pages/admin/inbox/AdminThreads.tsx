import { useState, type FormEvent } from "react";
import { useCloseThread, useReopenThread } from "../../../hooks/inbox/useThreads";
import { useStaffSendReply, useStaffThread, useStaffThreads } from "../../../hooks/inbox/useStaffInbox";
import { formatDate, formatTime } from "../../../utils/date";
import { btn, input, section } from "./adminStyles";

type StatusFilter = "" | "Open" | "Closed";

export default function AdminThreads() {
    const [page, setPage] = useState(1);
    const [draft, setDraft] = useState<{ customerId: string; status: StatusFilter; searchTerm: string }>({
        customerId: "",
        status: "",
        searchTerm: "",
    });
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

    const data = threads.data;
    const rows = data?.items ?? [];
    const busy = closeThread.isPending || reopenThread.isPending;

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
                    <select
                        className={input}
                        value={draft.status}
                        onChange={(e) => setDraft({ ...draft, status: e.target.value as StatusFilter })}
                    >
                        <option value="">Alla</option>
                        <option value="Open">Öppen</option>
                        <option value="Closed">Stängd</option>
                    </select>
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
                                <th className="p-1">Typ</th>
                                <th className="p-1">Status</th>
                                <th className="p-1">Senast</th>
                                <th className="p-1"></th>
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map((thread) => {
                                const isClosed = thread.status?.toLowerCase() === "closed";
                                return (
                                    <tr key={thread.id} className={`border-b ${thread.id === selectedId ? "bg-light-gray" : ""}`}>
                                        <td className="p-1">{thread.id}</td>
                                        <td className="p-1">{thread.customerName ?? "-"} ({thread.customerId})</td>
                                        <td className="p-1">{thread.subject ?? "(inget ämne)"}</td>
                                        <td className="p-1">{thread.isInformationOnly ? "Information" : "Konversation"}</td>
                                        <td className="p-1">{thread.status ?? "-"}</td>
                                        <td className="p-1">{formatDate(thread.lastMessageAt)}</td>
                                        <td className="p-1 flex gap-2">
                                            <button type="button" className={btn} onClick={() => setSelectedId(thread.id)}>
                                                Öppna
                                            </button>
                                            <button
                                                type="button"
                                                className={btn}
                                                disabled={busy}
                                                onClick={() => (isClosed ? reopenThread.mutate(thread.id) : closeThread.mutate(thread.id))}
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

            {data && data.totalPages > 1 && (
                <div className="flex items-center gap-3">
                    <button type="button" className={btn} disabled={!data.hasPreviousPage} onClick={() => setPage(data.page - 1)}>
                        Föregående
                    </button>
                    <span>Sida {data.page} av {data.totalPages}</span>
                    <button type="button" className={btn} disabled={!data.hasNextPage} onClick={() => setPage(data.page + 1)}>
                        Nästa
                    </button>
                </div>
            )}

            {selectedId !== undefined && <ThreadPanel key={selectedId} id={selectedId} />}
        </section>
    );
}

function ThreadPanel({ id }: { id: number }) {
    const thread = useStaffThread(id);
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
                        <strong>Status:</strong> {thread.data.status ?? "-"} |{" "}
                        <strong>Kategori:</strong> {thread.data.category ?? "-"} |{" "}
                        <strong>Kunden kan svara:</strong> {thread.data.canReply ? "Ja" : "Nej"}
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

                    {/* Reply only once staff can see what they answer. */}
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
                </>
            )}
        </div>
    );
}
