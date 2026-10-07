import { useEffect, useRef } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useNotifications, useSupportThreads, useUnreadCount } from "../../hooks/useInbox";
import { formatWhen } from "../../utils/date";
import { toNotificationViews } from "../inbox/inboxAdapters";

type InboxDropdownProps = {
    open: boolean;
    onOpenChange: (open: boolean) => void;
};

export default function InboxDropdown({ open, onOpenChange }: InboxDropdownProps) {
    const { t } = useTranslation();
    const ref = useRef<HTMLDivElement>(null);
    const buttonRef = useRef<HTMLButtonElement>(null);
    const reduceMotion = useReducedMotion();

    const { data: unreadCount = 0 } = useUnreadCount();
    const threads = useSupportThreads("inbox", 1, 5, open);
    const notifications = useNotifications(true, 1, 5, open);

    const unreadNotifications = toNotificationViews(notifications.data);
    const close = () => onOpenChange(false);

    useEffect(() => {
        if (!open) return;

        const handleClickOutside = (event: MouseEvent) => {
            if (ref.current && !ref.current.contains(event.target as Node)) {
                onOpenChange(false);
            }
        };
        // Escape closes and returns focus to trigger.
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key !== "Escape") return;
            onOpenChange(false);
            buttonRef.current?.focus();
        };
        // Tabbing out closes, so panel never covers focused element.
        const handleFocusIn = (event: FocusEvent) => {
            if (ref.current && !ref.current.contains(event.target as Node)) {
                onOpenChange(false);
            }
        };

        document.addEventListener("mousedown", handleClickOutside);
        document.addEventListener("keydown", handleKeyDown);
        document.addEventListener("focusin", handleFocusIn);
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
            document.removeEventListener("keydown", handleKeyDown);
            document.removeEventListener("focusin", handleFocusIn);
        };
    }, [open, onOpenChange]);

    return (
        <div ref={ref} className="flex">
            <button
                ref={buttonRef}
                type="button"
                title={t("inbox.title")}
                onClick={() => onOpenChange(!open)}
                aria-label={
                    unreadCount > 0
                        ? t("inbox.aria-label-unread", { count: unreadCount })
                        : t("inbox.title")
                }
                aria-expanded={open}
                aria-controls="inbox-panel"
                className="relative flex h-11 w-11 cursor-pointer items-end justify-end text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
            >
                <img src="/icons/inbox.svg" alt="" aria-hidden="true" className="h-7 w-7 invert" />
                {unreadCount > 0 && (
                    <span
                        aria-hidden="true"
                        className="absolute right-[-6px] top-2 rounded-full bg-nordiska-orange px-1.5 text-xs font-bold text-dark-navy"
                    >
                        {unreadCount > 99 ? "99+" : unreadCount}
                    </span>
                )}
            </button>

            <AnimatePresence>
                {open && (
                    <motion.div
                        id="inbox-panel"
                        initial={{ height: 0, opacity: 0 }}
                        animate={{ height: "auto", opacity: 1 }}
                        exit={{ height: 0, opacity: 0 }}
                        transition={{ duration: reduceMotion ? 0 : 0.25, ease: "easeOut" }}
                        className="fixed inset-x-0 top-[60px] overflow-hidden bg-white font-montserrat text-sm text-dark-navy shadow-floating md:left-auto md:right-5 md:top-[75px] md:w-80"
                    >
                        <div className="max-h-[calc(100dvh-60px)] overflow-y-auto md:max-h-[calc(100dvh-75px)]">
                            <div className="bg-dark-navy px-4 py-3 text-white">
                                <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.title")}</span>
                                <strong>{t("inbox.unread-count", { count: unreadCount })}</strong>
                            </div>

                            <p
                                id="inbox-panel-messages"
                                className="px-4 pt-3 text-xs font-semibold uppercase text-secondary"
                            >
                                {t("inbox.messages")}
                            </p>

                            {threads.isPending && (
                                <p role="status" className="px-4 py-3 text-secondary">{t("inbox.loading")}</p>
                            )}

                            {threads.isError && (
                                <div className="flex items-center justify-between px-4 py-3">
                                    <span role="alert" className="text-error">{t("inbox.error.load")}</span>
                                    <button
                                        type="button"
                                        onClick={() => threads.refetch()}
                                        className="cursor-pointer text-primary-blue underline"
                                    >
                                        {t("inbox.try-again")}
                                    </button>
                                </div>
                            )}

                            {threads.data && threads.data.items.length === 0 && (
                                <p className="px-4 py-3 text-secondary">{t("inbox.empty.messages")}</p>
                            )}

                            {threads.data && threads.data.items.length > 0 && (
                                <ul aria-labelledby="inbox-panel-messages" className="mt-2">
                                    {threads.data.items.map((thread) => (
                                        <li key={thread.id} className="border-t border-light-blue-accent/50">
                                            <Link
                                                to={`/inbox/${thread.id}`}
                                                onClick={close}
                                                className={`block px-4 py-3 ${thread.isRead ? "" : "bg-light-blue-accent/20 font-bold"}`}
                                            >
                                                <span className="block text-xs font-normal text-secondary">
                                                    {formatWhen(thread.lastMessageAt)} · {t("inbox.message-count", { count: thread.messageCount })}
                                                </span>
                                                {!thread.isRead && <span className="sr-only">{t("inbox.unread-prefix")}</span>}
                                                {thread.subject ?? t("inbox.no-subject")}
                                            </Link>
                                        </li>
                                    ))}
                                </ul>
                            )}

                            <p
                                id="inbox-panel-notifications"
                                className="border-t border-light-blue-accent/50 px-4 pt-3 text-xs font-semibold uppercase text-secondary"
                            >
                                {t("inbox.notifications")}
                            </p>

                            {notifications.isPending && (
                                <p role="status" className="px-4 py-3 text-secondary">{t("inbox.loading")}</p>
                            )}

                            {notifications.isError && (
                                <div className="flex items-center justify-between px-4 py-3">
                                    <span role="alert" className="text-error">{t("inbox.error.load")}</span>
                                    <button
                                        type="button"
                                        onClick={() => notifications.refetch()}
                                        className="cursor-pointer text-primary-blue underline"
                                    >
                                        {t("inbox.try-again")}
                                    </button>
                                </div>
                            )}

                            {notifications.isSuccess && unreadNotifications.length === 0 && (
                                <p className="px-4 py-3 text-secondary">{t("inbox.empty.new-notifications")}</p>
                            )}

                            {unreadNotifications.length > 0 && (
                                <ul aria-labelledby="inbox-panel-notifications" className="mt-2">
                                    {unreadNotifications.map((item) => (
                                        <li key={item.id} className="border-t border-light-blue-accent/50">
                                            <Link
                                                to="/inbox?folder=notifications"
                                                onClick={close}
                                                className="block bg-light-blue-accent/20 px-4 py-3 font-bold"
                                            >
                                                <span className="block text-xs font-normal text-secondary">
                                                    {formatWhen(item.date)}
                                                </span>
                                                <span className="sr-only">{t("inbox.unread-prefix")}</span>
                                                {item.title ?? t("inbox.untitled")}
                                            </Link>
                                        </li>
                                    ))}
                                </ul>
                            )}

                            <div className="border-t border-light-blue-accent/50 p-4">
                                <Link
                                    to="/inbox"
                                    onClick={close}
                                    className="block rounded bg-primary-blue px-4 py-2 text-center font-semibold text-white"
                                >
                                    {t("inbox.open-inbox")}
                                </Link>
                            </div>
                        </div>
                    </motion.div>
                )}
            </AnimatePresence>
        </div>
    );
}
