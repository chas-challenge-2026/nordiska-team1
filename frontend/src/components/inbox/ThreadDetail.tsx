import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import {
    useCustomerSendReply,
    useMarkAllAsRead,
    useMoveThreadToArchive,
    useRestoreThreadFromArchive,
    useSupportThread,
} from "../../hooks/useInbox";
import type { Folders } from "../../services/inboxService";
import { formatDate, formatTime } from "../../utils/date";
import { useUserStore } from "../../store/userStore";

type ThreadDetailProps = {
    id: number;
    search: string;
    folder: Folders;
};

const btn = "cursor-pointer rounded bg-primary-blue px-4 py-2 font-semibold text-white disabled:cursor-default disabled:opacity-40";

export default function ThreadDetail({ id, search, folder }: ThreadDetailProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const thread = useSupportThread(id);
    const { mutate: markAsRead } = useMarkAllAsRead();
    const sendReply = useCustomerSendReply();
    const archive = useMoveThreadToArchive();
    const restore = useRestoreThreadFromArchive();
    const [body, setBody] = useState("");
    const [sent, setSent] = useState(false);
    const markedId = useRef<number | null>(null);
    const headingRef = useRef<HTMLHeadingElement>(null);
    const { user } = useUserStore();

    const data = thread.data;
    const loaded = data !== undefined;
    const needsRead = loaded && !data.isRead;

    // Mark as read once per opened thread.
    useEffect(() => {
        if (!needsRead || markedId.current === id) return;
        markedId.current = id;
        markAsRead(id);
    }, [id, needsRead, markAsRead]);

    // Move focus to opened thread; list is hidden on mobile.
    useEffect(() => {
        if (loaded) headingRef.current?.focus();
    }, [loaded]);

    if (thread.isPending) {
        return <p role="status" className="bg-white px-4 py-3 text-secondary shadow-card">{t("inbox.loading")}</p>;
    }

    if (thread.isError || !data) {
        return (
            <div className="flex items-center justify-between bg-white px-4 py-3 shadow-card">
                <span role="alert" className="text-error">{t("inbox.error.load")}</span>
                <button type="button" onClick={() => thread.refetch()} className="cursor-pointer text-primary-blue underline">
                    {t("inbox.try-again")}
                </button>
            </div>
        );
    }

    // Tab is authoritative; backend value checked case-insensitively as fallback.
    const isArchived = folder === "archive" || data.folder?.toLowerCase() === "archive";
    const move = isArchived ? restore : archive;

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const trimmed = body.trim();
        if (!trimmed) return;
        sendReply.mutate(
            { id, body: trimmed },
            { onSuccess: () => { setBody(""); setSent(true); } },
        );
    };

    return (
        <div className="bg-white shadow-card">
            <div className="flex justify-between gap-3 bg-dark-navy px-4 py-3 text-white">
                <div className="min-w-0">
                    <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.subject")}</span>
                    <h2 ref={headingRef} tabIndex={-1} className="break-words font-bold">
                        {data.subject ?? t("inbox.no-subject")}
                    </h2>
                </div>
                {data.status && (
                    <div className="text-right">
                        <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.thread.status")}</span>
                        <strong>{data.status}</strong>
                    </div>
                )}
            </div>

            <ul aria-label={t("inbox.messages")} className="flex flex-col gap-3 p-4">
                {data.messages.map((message) => {
                    const mine = message.senderCustomerId === user?.id;
                    return (
                        <li
                            key={message.id}
                            className={`max-w-[85%] border border-light-blue-accent/50 px-3 py-2 ${mine ? "self-end bg-light-blue-accent/20" : "self-start"}`}
                        >
                            <span className="block text-xs text-secondary">
                                {mine ? t("inbox.thread.you") : message.senderName ?? t("inbox.thread.support")} · {formatDate(message.sentAt)} {formatTime(message.sentAt)}
                            </span>
                            <p className="whitespace-pre-wrap break-words">{message.body}</p>
                        </li>
                    );
                })}
            </ul>

            {data.canReply && (
                <form onSubmit={handleSubmit} className="flex flex-col gap-2 border-t border-light-blue-accent/50 p-4">
                    <label htmlFor="reply-body" className="text-xs font-semibold uppercase text-secondary">
                        {t("inbox.thread.reply")}
                    </label>
                    <textarea
                        id="reply-body"
                        required
                        rows={3}
                        value={body}
                        onChange={(event) => { setBody(event.target.value); setSent(false); }}
                        className="w-full border border-secondary p-2"
                    />
                    <p role="status" className="sr-only">{sent ? t("inbox.thread.reply-sent") : ""}</p>
                    {sendReply.isError && <span role="alert" className="text-error">{t("inbox.error.action")}</span>}
                    <button type="submit" className={`${btn} md:self-end`} disabled={sendReply.isPending}>
                        {sendReply.isPending ? t("inbox.sending") : t("inbox.send")}
                    </button>
                </form>
            )}

            <div className="flex flex-col gap-2 border-t border-light-blue-accent/50 p-4 md:flex-row md:items-center">
                <button
                    type="button"
                    className={btn}
                    disabled={move.isPending}
                    onClick={() => move.mutate(id, { onSuccess: () => navigate({ pathname: "/inbox", search }) })}
                >
                    {isArchived ? t("inbox.thread.restore") : t("inbox.thread.archive")}
                </button>
                {move.isError && <span role="alert" className="text-error">{t("inbox.error.action")}</span>}
            </div>
        </div>
    );
}
