import { useEffect, useRef } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useInboxOverview, useInboxCounts } from "../../hooks/inbox/useInboxSummary";
import type { FeedItem } from "../../types/inbox";
import { formatWhen } from "../../utils/date";
import { InboxLoading, InboxLoadError } from "../inbox/InboxState";

type InboxDropdownProps = {
    open: boolean;
    onOpenChange: (open: boolean) => void;
};

const MAX_ITEMS = 6;

/** Where a feed item leads. Threads open directly; other types open their tab. */
function hrefFor(item: FeedItem): string {
    switch (item.type) {
        case "thread": return `/inbox/${item.sourceId}`;
        case "notification": return "/inbox?folder=notifications";
        case "document": return "/inbox?folder=documents";
        case "term": return "/inbox?folder=terms";
        default: return "/inbox";
    }
}

export default function InboxDropdown({ open, onOpenChange }: InboxDropdownProps) {
    const { t } = useTranslation();
    const ref = useRef<HTMLDivElement>(null);
    const buttonRef = useRef<HTMLButtonElement>(null);
    const reduceMotion = useReducedMotion();

    const { data: counts } = useInboxCounts();
    const unreadCount = counts?.unreadCount ?? 0;
    const overview = useInboxOverview(open);
    const items = overview.data?.feed.slice(0, MAX_ITEMS) ?? [];

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
                                <span className="block text-xs uppercase text-white/80">{t("inbox.title")}</span>
                                <strong>{t("inbox.unread-count", { count: unreadCount })}</strong>
                            </div>

                            {overview.isPending && <InboxLoading />}
                            {overview.isError && <InboxLoadError onRetry={() => overview.refetch()} />}

                            {overview.isSuccess && items.length === 0 && (
                                <p className="px-4 py-3 text-secondary">{t("inbox.empty.feed")}</p>
                            )}

                            {items.length > 0 && (
                                <ul aria-label={t("inbox.title")}>
                                    {items.map((item) => (
                                        <li
                                            key={`${item.type}-${item.id}`}
                                            className="border-t border-light-blue-accent/50 first:border-t-0"
                                        >
                                            <Link
                                                to={hrefFor(item)}
                                                onClick={close}
                                                className={`block px-4 py-3 ${item.isRead ? "" : "bg-light-blue-accent/20"}`}
                                            >
                                                <span className="flex justify-between gap-2 text-xs text-secondary">
                                                    <span>{item.type ? t(`inbox.feed-type.${item.type}`) : ""}</span>
                                                    <span>{formatWhen(item.occuredAt)}</span>
                                                </span>
                                                <span className={`block break-words ${item.isRead ? "" : "font-bold"}`}>
                                                    {!item.isRead && <span className="sr-only">{t("inbox.unread-prefix")}</span>}
                                                    {item.title ?? t("inbox.untitled")}
                                                </span>
                                                {item.preview && (
                                                    <span className="line-clamp-2 block break-words text-xs text-secondary">
                                                        {item.preview}
                                                    </span>
                                                )}
                                                {item.actionRequired && (
                                                    <span className="mt-1 inline-block rounded bg-nordiska-orange px-1.5 text-xs font-semibold text-dark-navy">
                                                        {t("inbox.action-required")}
                                                    </span>
                                                )}
                                            </Link>
                                        </li>
                                    ))}
                                </ul>
                            )}

                            <div className="border-t border-light-blue-accent/50 p-4">
                                <Link
                                    to="/inbox"
                                    onClick={close}
                                    className="block rounded bg-primary-blue px-4 py-2 text-center font-semibold text-white hover:bg-primary-hover"
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
