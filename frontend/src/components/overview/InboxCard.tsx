
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { useInboxOverview } from "../../hooks/inbox/useInboxSummary";
import type { FeedItem } from "../../types/inbox";
import { formatDate} from "../../utils/date";
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
                <section className="relative flex min-h-0 flex-1 flex-col overflow-hidden">
                    <div className="min-h-0 flex-1 overflow-y-auto">
                        {items.map((item) => (
                            <Link
                                key={`${item.type}-${item.sourceId}`}
                                to={hrefFor(item)}
                                className={`flex w-full shrink-0 flex-col justify-center py-3 px-3 ${item.isRead ? "bg-white" : "bg-light-gray/60"} hover:bg-primary-blue/10 cursor-pointer border-b border-light-blue-accent/50 h-1/4 last:mb-15`}
                            >
                                <p className={`${item.isRead ? "" : "font-semibold"}`}>{item.title}</p>
                                <p className="text-xs xl:text-sm">
                                    {item.actionRequired ? <span className="font-semibold bg-nordiska-orange px-1 rounded">{t("inbox.action-required")}</span> : <span className="text-dark-navy/60">{formatDate(item.createdAt)}</span>}
                                </p>
                            </Link>
                        ))}
                    </div>

                    {/* LINK TO INBOX */}
                    <div className="absolute bottom-0 w-full shrink-0 border-t border-primary-hover/50 p-3 bg-[#D0E5FF] ">
                        <Link to="/inbox" className="block rounded border border-primary-blue px-4 py-1 bg-white text-primary-blue text-base text-center font-medium  transition-colors hover:bg-primary-blue hover:text-white">
                            {t("inbox.open-inbox")}
                        </Link>
                    </div>
                </section>
            </div>
        </OverviewCard>
    );
}