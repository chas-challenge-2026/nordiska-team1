import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useSupportThreads, usePrefetchSupportThread } from "../../hooks/inbox/useThreads";
import type { Folders } from "../../types/inbox";
import { formatWhen } from "../../utils/date";
import PagePagination from "../PagePagination";

type ThreadListProps = {
    folder: Folders;
    page: number;
    search: string;
    selectedId?: number;
    onPageChange: (page: number) => void;
};

export default function ThreadList({ folder, page, search, selectedId, onPageChange }: ThreadListProps) {
    const { t } = useTranslation();
    const threads = useSupportThreads(folder, page, 10);
    const prefetch = usePrefetchSupportThread();
    const data = threads.data;

    return (
        <div className="bg-white shadow-card">
            <div className="bg-dark-navy px-4 py-3 text-white">
                <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.view-message")}</span>
                <h2 className="font-bold">{t("inbox.select-message")}</h2>
            </div>

            {threads.isPending && <p role="status" className="px-4 py-3 text-secondary">{t("generic.loading")}</p>}

            {threads.isError && (
                <div className="flex items-center justify-between px-4 py-3">
                    <span role="alert" className="text-error">{t("generic.error-load")}</span>
                    <button type="button" onClick={() => threads.refetch()} className="cursor-pointer text-primary-blue underline">
                        {t("generic.try-again")}
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
                                onMouseEnter={() => prefetch(thread.id)}
                                onFocus={() => prefetch(thread.id)}
                                aria-current={thread.id === selectedId ? "true" : undefined}
                                className={`flex justify-between gap-2 px-4 py-3 ${thread.id === selectedId ? "bg-light-blue-accent/20" : ""} ${thread.isRead ? "" : "font-bold"}`}
                            >
                                <span className="min-w-0">
                                    <span className="block text-xs font-normal text-secondary">
                                        {formatWhen(thread.lastMessageAt)}
                                        {" · "}
                                        {thread.isInformationOnly
                                            ? t("generic.information")
                                            : t("inbox.message-count", { count: thread.messageCount })}
                                    </span>
                                    <span className="block break-words">
                                        {!thread.isRead && <span className="sr-only">{t("inbox.unread-prefix")}</span>}
                                        {thread.subject ?? t("inbox.no-subject")}
                                    </span>
                                </span>
                                {thread.status && !thread.isInformationOnly && (
                                    <span className="text-xs font-normal text-secondary">
                                        {t(`inbox.status.${thread.status.toLowerCase()}`, { defaultValue: thread.status })}
                                    </span>
                                )}
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
