import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import {
    useCustomerSendReply,
    useMarkThreadAsRead,
    useMoveThreadToArchive,
    useRestoreThreadFromArchive,
    useSupportThread,
} from "../../hooks/inbox/useThreads";
import type { Folders, SupportTicketThread } from "../../types/inbox";
import { formatDate, formatTime } from "../../utils/date";
import { useUserStore } from "../../store/userStore";
import InboxButton from "./InboxButton";
import { InboxLoadError } from "./InboxState";

type ThreadDetailProps = {
    id: number;
    search: string;
    folder: Folders;
};


export default function ThreadDetail({ id, search, folder }: ThreadDetailProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const thread = useSupportThread(id);
    const { mutate: markAsRead } = useMarkThreadAsRead();
    const archive = useMoveThreadToArchive();
    const restore = useRestoreThreadFromArchive();
    const markedId = useRef<number | null>(null);
    const headingRef = useRef<HTMLHeadingElement>(null);

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
        return (
            <div className="bg-white shadow-card" aria-busy="true">
                <div className="bg-dark-navy px-4 py-3">
                    {/* Invisible lines match real header height. */}
                    <span className="block text-xs">&nbsp;</span>
                    <span className="block font-bold">&nbsp;</span>
                </div>
                <div className="min-h-48 p-4">
                    <p role="status" className="text-secondary">{t("generic.loading")}</p>
                </div>
            </div>
        );
    }

    if (thread.isError || !data) {
        return (
            <div className="bg-white shadow-card">
                <InboxLoadError onRetry={() => thread.refetch()} />
            </div>
        );
    }

    // Tab is authoritative; backend value checked case-insensitively as fallback.
    const isArchived = folder === "archive" || data.folder?.toLowerCase() === "archive";
    const move = isArchived ? restore : archive;

    return (
        <div className="bg-white shadow-card">
            <div className="flex justify-between gap-3 bg-dark-navy px-4 py-3 text-white">
                <div className="min-w-0">
                    <span className="block text-xs uppercase text-light-blue-accent">
                        {data.isInformationOnly ? t("generic.information") : t("inbox.subject")}
                    </span>
                    <h2 ref={headingRef} tabIndex={-1} className="break-words font-bold">
                        {data.subject ?? t("inbox.no-subject")}
                    </h2>
                </div>
                {data.status && !data.isInformationOnly && (
                    <div className="text-right">
                        <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.thread.status")}</span>
                        <strong>{t(`inbox.status.${data.status.toLowerCase()}`, { defaultValue: data.status })}</strong>
                    </div>
                )}
            </div>

            {data.isInformationOnly ? (
                <InformationMessage thread={data} />
            ) : (
                <>
                    <Conversation thread={data} />
                    {data.canReply && <ReplyForm threadId={id} />}
                </>
            )}

            <div className="flex flex-col gap-2 border-t border-light-blue-accent/50 p-4 md:flex-row md:items-center">
                <InboxButton
                    disabled={move.isPending}
                    onClick={() => move.mutate(id, { onSuccess: () => navigate({ pathname: "/inbox", search }) })}
                >
                    {isArchived ? t("inbox.thread.restore") : t("inbox.thread.archive")}
                </InboxButton>
                {move.isError && <span role="alert" className="text-error">{t("generic.error-action")}</span>}
            </div>
        </div>
    );
}

/** Broadcast / announcement: one plain box, no chat layout. */
function InformationMessage({ thread }: { thread: SupportTicketThread }) {
    const { t } = useTranslation();
    return (
        <div className="flex flex-col gap-4 p-4">
            {thread.messages.map((message) => (
                <article key={message.id} className="border border-light-blue-accent/50 p-4">
                    <p className="mb-2 text-xs text-secondary">
                        {message.senderName ?? t("inbox.thread.support")} · {formatDate(message.sentAt)} {formatTime(message.sentAt)}
                    </p>
                    <p className="whitespace-pre-wrap break-words">{message.body}</p>
                </article>
            ))}
        </div>
    );
}

/** Chat layout. Own messages right, others left. */
function Conversation({ thread }: { thread: SupportTicketThread }) {
    const { t } = useTranslation();
    const { user } = useUserStore();
    return (
        <ul aria-label={t("inbox.messages")} className="flex flex-col gap-3 p-4">
            {thread.messages.map((message) => {
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
    );
}

function ReplyForm({ threadId }: { threadId: number }) {
    const { t } = useTranslation();
    const sendReply = useCustomerSendReply();
    const [body, setBody] = useState("");
    const [sent, setSent] = useState(false);

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const trimmed = body.trim();
        if (!trimmed) return;
        sendReply.mutate(
            { id: threadId, body: trimmed },
            { onSuccess: () => { setBody(""); setSent(true); } },
        );
    };

    return (
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
            {sendReply.isError && <span role="alert" className="text-error">{t("generic.error-action")}</span>}
            <InboxButton type="submit" className="md:self-end" disabled={sendReply.isPending}>
                {sendReply.isPending ? t("generic.sending") : t("generic.send")}
            </InboxButton>
        </form>
    );
}
