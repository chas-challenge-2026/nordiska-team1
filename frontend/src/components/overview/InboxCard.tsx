
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { useInboxOverview } from "../../hooks/inbox/useInboxSummary";
import type { FeedItem } from "../../types/inbox";
import { formatWhen } from "../../utils/date";
import { LoadingState, ErrorState, EmptyInboxState } from "../StatusMessage";

import OverviewCardHeader from "./OverviewCardHeader";

const MAX_ITEMS = 5;

function hrefFor(item: FeedItem): string {
    switch (item.type) {
        case "thread":
            return `/inbox/${item.sourceId}`;
        case "notification":
            return "/inbox?folder=notifications";
        case "document":
            return "/inbox?folder=documents";
        case "term":
            return "/inbox?folder=terms";
        default:
            return "/inbox";
    }
}

export default function InboxCard() {
    const { t } = useTranslation();
    const overview = useInboxOverview(true);
    const items = overview.data?.feed.slice(0, MAX_ITEMS) ?? [];

    return (
        <OverviewCard>
            <div className="flex h-full flex-col">

                {/* CARD HEADER */}
                <OverviewCardHeader 
                    title={t("overview-route.inbox-card.title")}
                    description={t("overview-route.inbox-card.description")}
                />

                {/* USER FEEDBACK */}
                {overview.isPending && <LoadingState title={t("overview-route.inbox-card.pending")}/>}
                {overview.isError && (<ErrorState title={t("overview-route.inbox-card.error")} onRetry={() => overview.refetch()}/>)}
                {overview.isSuccess && items.length === 0 && ( <EmptyInboxState title={t("inbox.empty.feed")}/>)}


                {/* CONTENT */}
                <section className="flex flex-1 flex-col overflow-hidden">
                    <div className="flex flex-1 flex-col overflow-y-auto">

                        {items.length > 0 && (
                            <ul aria-label={t("overview-route.inbox-card.aria-label")}>
                                {items.map((item) => (
                                    <li key={`${item.type}-${item.id}`} className="border-b border-light-blue-accent/50 last:border-b-0">

                                        <Link to={hrefFor(item)} className={`block rounded px-3 py-3 xl:py-4 -mt-[1px] transition-colors hover:bg-light-blue-accent/20 ${item.isRead? "bg-light-gray/60" : ""}`}>

                                            <div className="flex items-start justify-between gap-3">
                                                <span className="min-w-0 break-words text-sm xl:text-base">
                                                    {!item.isRead && ( <span className="sr-only"> {t("inbox.unread-prefix")} </span>)}
                                                    <span className={item.isRead ? "" : "font-semibold"}>{item.title ?? t("inbox.untitled")}</span>
                                                </span>
                                                <time className="shrink-0 text-xs text-secondary">{formatWhen(item.occuredAt)}</time>
                                            </div>

                                            {item.preview && (<p className="line-clamp-2 break-words text-xs text-secondary">{item.preview}</p>)}

                                            {item.actionRequired && (
                                                <span className="mt-2 inline-block rounded bg-nordiska-orange px-1.5 py-0.5 text-xs font-semibold text-dark-navy">
                                                    {t("inbox.action-required")}
                                                </span>
                                            )}
                                        </Link>
                                    </li>
                                ))}
                            </ul>
                        )}
                    </div>

                    {/* LINK TO INBOX */}
                    <div className=" shrink-0 border-t border-primary-hover/50 p-3 bg-light-blue-accent/40 ">
                        <Link to="/inbox" className="block rounded border border-primary-blue px-4 py-1 bg-white text-primary-blue text-base text-center font-medium  transition-colors hover:bg-primary-blue hover:text-white">
                            {t("inbox.open-inbox")}
                        </Link>
                    </div>
                </section>
            </div>
        </OverviewCard>
    );
}