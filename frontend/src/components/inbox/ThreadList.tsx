import { useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useMarkAllAsRead, useSupportThreads } from "../../hooks/useInbox";
import type { Folders } from "../../services/inboxService";
import { formatWhen } from "../../utils/date";
import PagePagination from "../PagePagination";

type ThreadListProps = {
    folder: Folders;
    page: number;
    search: string;
    selectedId?: number;
    onPageChange: (page: number) => void;
};

const headBtn = "cursor-pointer rounded border border-white px-3 py-1 text-xs font-semibold text-white disabled:cursor-default disabled:opacity-40";

export default function ThreadList({ folder, page, search, selectedId, onPageChange }: ThreadListProps) {
    const { t } = useTranslation();
    const threads = useSupportThreads(folder, page, 10);
    const markRead = useMarkAllAsRead();
    const [marking, setMarking] = useState(false);
    const [markFailed, setMarkFailed] = useState(false);

    const data = threads.data;
    const unread = data?.items.filter((thread) => !thread.isRead) ?? [];

    // No bulk endpoint: marks each unread thread on current page.
    const handleMarkAll = async () => {
        setMarking(true);
        setMarkFailed(false);
        const results = await Promise.allSettled(unread.map((thread) => markRead.mutateAsync(thread.id)));
        setMarkFailed(results.some((result) => result.status === "rejected"));
        setMarking(false);
    };

    return (
        <div className="bg-white shadow-card">
            <div className="flex items-center justify-between gap-3 bg-dark-navy px-4 py-3 text-white">
                <div>
                    <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.view-message")}</span>
                    <h2 className="font-bold">{t("inbox.select-message")}</h2>
                </div>
                {folder === "inbox" && (
                    <button
                        type="button"
                        className={headBtn}
                        disabled={unread.length === 0 || marking}
                        onClick={handleMarkAll}
                    >
                        {t("inbox.mark-all-read")}
                    </button>
                )}
            </div>

            {markFailed && <p role="alert" className="px-4 py-2 text-error">{t("inbox.error.action")}</p>}

            {threads.isPending && <p role="status" className="px-4 py-3 text-secondary">{t("inbox.loading")}</p>}

            {threads.isError && (
                <div className="flex items-center justify-between px-4 py-3">
                    <span role="alert" className="text-error">{t("inbox.error.load")}</span>
                    <button type="button" onClick={() => threads.refetch()} className="cursor-pointer text-primary-blue underline">
                        {t("inbox.try-again")}
                    </button>
                </div>
            )}

            {data && data.items.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.messages")}</p>
            )}

            {data && data.items.length > 0 && (
                <ul>
                    {data.items.map((thread) => (
                        <li key={thread.id} className="border-t border-light-blue-accent/50">
                            <Link
                                to={{ pathname: `/inbox/${thread.id}`, search }}
                                aria-current={thread.id === selectedId ? "true" : undefined}
                                className={`flex justify-between gap-2 px-4 py-3 ${thread.id === selectedId ? "bg-light-blue-accent/20" : ""} ${thread.isRead ? "" : "font-bold"}`}
                            >
                                <span className="min-w-0">
                                    <span className="block text-xs font-normal text-secondary">
                                        {formatWhen(thread.lastMessageAt)} · {t("inbox.message-count", { count: thread.messageCount })}
                                    </span>
                                    <span className="block break-words">
                                        {!thread.isRead && <span className="sr-only">{t("inbox.unread-prefix")}</span>}
                                        {thread.subject ?? t("inbox.no-subject")}
                                    </span>
                                </span>
                                {thread.status && <span className="text-xs font-normal text-secondary">{thread.status}</span>}
                            </Link>
                        </li>
                    ))}
                </ul>
            )}

            {data && data.totalPages > 1 && (
                <div className="border-t border-light-blue-accent/50 p-4">
                    <PagePagination
                        page={data.page}
                        totalPages={data.totalPages}
                        hasPreviousPage={data.hasPreviousPage}
                        hasNextPage={data.hasNextPage}
                        onPrevious={() => onPageChange(data.page - 1)}
                        onNext={() => onPageChange(data.page + 1)}
                        onPageChange={onPageChange}
                        isDisabled={threads.isPlaceholderData}
                    />
                </div>
            )}
        </div>
    );
}
