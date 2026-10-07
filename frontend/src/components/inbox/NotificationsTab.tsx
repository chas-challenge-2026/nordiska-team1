import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useMarkAllNotificationsAsRead,
    useMarkNotificationAsRead,
    useNotifications,
} from "../../hooks/useInbox";
import { formatWhen } from "../../utils/date";
import PagePagination from "../PagePagination";
import { pagingOf, toNotificationViews } from "./inboxAdapters";

type NotificationsTabProps = {
    page: number;
    onPageChange: (page: number) => void;
};

const headBtn = "cursor-pointer rounded border border-white px-3 py-1 text-xs font-semibold text-white disabled:cursor-default disabled:opacity-40";

export default function NotificationsTab({ page, onPageChange }: NotificationsTabProps) {
    const { t } = useTranslation();
    const [unreadOnly, setUnreadOnly] = useState(false);
    const notifications = useNotifications(unreadOnly, page, 10);
    const markOne = useMarkNotificationAsRead();
    const markAll = useMarkAllNotificationsAsRead();

    const items = toNotificationViews(notifications.data);
    const paging = pagingOf(notifications.data);
    const hasUnread = items.some((item) => !item.isRead);

    return (
        <div className="bg-white shadow-card">
            <div className="flex items-center justify-between gap-3 bg-dark-navy px-4 py-3 text-white">
                <div>
                    <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.title")}</span>
                    <h2 className="font-bold">{t("inbox.notifications")}</h2>
                </div>
                <button
                    type="button"
                    className={headBtn}
                    disabled={!hasUnread || markAll.isPending}
                    onClick={() => markAll.mutate()}
                >
                    {t("inbox.mark-all-read")}
                </button>
            </div>

            <div className="flex items-center gap-2 p-4">
                <input
                    id="notifications-unread-only"
                    type="checkbox"
                    checked={unreadOnly}
                    onChange={(event) => {
                        setUnreadOnly(event.target.checked);
                        onPageChange(1);
                    }}
                />
                <label htmlFor="notifications-unread-only">{t("inbox.notifications-tab.unread-only")}</label>
            </div>

            {(markOne.isError || markAll.isError) && (
                <p role="alert" className="px-4 py-2 text-error">{t("inbox.error.action")}</p>
            )}

            {notifications.isPending && <p role="status" className="px-4 py-3 text-secondary">{t("inbox.loading")}</p>}

            {notifications.isError && (
                <div className="flex items-center justify-between px-4 py-3">
                    <span role="alert" className="text-error">{t("inbox.error.load")}</span>
                    <button type="button" onClick={() => notifications.refetch()} className="cursor-pointer text-primary-blue underline">
                        {t("inbox.try-again")}
                    </button>
                </div>
            )}

            {notifications.isSuccess && items.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.notifications")}</p>
            )}

            {items.length > 0 && (
                <ul>
                    {items.map((item) => {
                        const title = item.title ?? t("inbox.untitled");
                        return (
                            <li
                                key={item.id}
                                className={`flex flex-wrap items-start justify-between gap-2 border-t border-light-blue-accent/50 px-4 py-3 ${item.isRead ? "" : "bg-light-blue-accent/20"}`}
                            >
                                <div className="min-w-0">
                                    <span className="block text-xs text-secondary">{formatWhen(item.date)}</span>
                                    <span className={`block break-words ${item.isRead ? "" : "font-bold"}`}>
                                        {!item.isRead && <span className="sr-only">{t("inbox.unread-prefix")}</span>}
                                        {title}
                                    </span>
                                    {item.body && <p className="whitespace-pre-wrap break-words">{item.body}</p>}
                                </div>
                                {!item.isRead && (
                                    <button
                                        type="button"
                                        className="cursor-pointer text-primary-blue underline disabled:cursor-default disabled:opacity-40"
                                        disabled={markOne.isPending}
                                        aria-label={t("inbox.mark-read-aria", { title })}
                                        onClick={() => markOne.mutate(item.id)}
                                    >
                                        {t("inbox.mark-read")}
                                    </button>
                                )}
                            </li>
                        );
                    })}
                </ul>
            )}

            {paging && paging.totalPages > 1 && (
                <div className="border-t border-light-blue-accent/50 p-4">
                    <PagePagination
                        page={paging.page}
                        totalPages={paging.totalPages}
                        hasPreviousPage={paging.hasPreviousPage}
                        hasNextPage={paging.hasNextPage}
                        onPrevious={() => onPageChange(paging.page - 1)}
                        onNext={() => onPageChange(paging.page + 1)}
                        onPageChange={onPageChange}
                        isDisabled={notifications.isPlaceholderData}
                    />
                </div>
            )}
        </div>
    );
}
