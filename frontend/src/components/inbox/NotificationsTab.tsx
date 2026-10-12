import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useMarkAllNotificationsAsRead,
    useMarkNotificationAsRead,
    useNotifications,
} from "../../hooks/inbox/useNotifications";
import { formatWhen } from "../../utils/date";
import PagePagination from "../PagePagination";
import InboxButton from "./InboxButton";
import { InboxLoading, InboxLoadError } from "./InboxState";

type NotificationsTabProps = {
    page: number;
    onPageChange: (page: number) => void;
};

export default function NotificationsTab({ page, onPageChange }: NotificationsTabProps) {
    const { t } = useTranslation();
    const [unreadOnly, setUnreadOnly] = useState(false);
    const notifications = useNotifications(unreadOnly, page, 10);
    const markOne = useMarkNotificationAsRead();
    const markAll = useMarkAllNotificationsAsRead();

    const data = notifications.data;
    const items = data?.items ?? [];
    const hasUnread = items.some((item) => !item.isRead);

    return (
        <div className="bg-white shadow-card">
            <div className="flex items-center justify-between gap-3 bg-dark-navy px-4 py-3 text-white">
                <div>
                    <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.title")}</span>
                    <h2 className="font-bold">{t("inbox.notifications")}</h2>
                </div>
                <InboxButton variant="outline-light" disabled={!hasUnread || markAll.isPending} onClick={() => markAll.mutate()}>
                    {t("inbox.mark-all-read")}
                </InboxButton>
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
                <p role="alert" className="px-4 py-2 text-error">{t("generic.error-action")}</p>
            )}

            {notifications.isPending && <InboxLoading />}
            {notifications.isError && <InboxLoadError onRetry={() => notifications.refetch()} />}

            {notifications.isSuccess && items.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.notifications")}</p>
            )}

            {items.length > 0 && (
                <ul>
                    {items.map((item) => {
                        // Without title, body is shown as title.
                        const title = item.title ?? item.body ?? t("inbox.untitled");
                        const body = item.title ? item.body : null;
                        return (
                            <li
                                key={item.id}
                                className={`flex flex-wrap items-start justify-between gap-2 border-t border-light-blue-accent/50 px-4 py-3 ${item.isRead ? "" : "bg-light-blue-accent/20"}`}
                            >
                                <div className="min-w-0">
                                    <span className="block text-xs text-secondary">{formatWhen(item.createdAt)}</span>
                                    <span className={`block break-words ${item.isRead ? "" : "font-bold"}`}>
                                        {!item.isRead && <span className="sr-only">{t("inbox.unread-prefix")}</span>}
                                        {title}
                                    </span>
                                    {body && <p className="whitespace-pre-wrap break-words">{body}</p>}
                                </div>
                                {!item.isRead && (
                                    <InboxButton
                                        variant="link"
                                        disabled={markOne.isPending}
                                        aria-label={t("inbox.mark-read-aria", { title })}
                                        onClick={() => markOne.mutate(item.id)}
                                    >
                                        {t("inbox.mark-read")}
                                    </InboxButton>
                                )}
                            </li>
                        );
                    })}
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
                        isDisabled={notifications.isPlaceholderData}
                    />
                </div>
            )}
        </div>
    );
}
