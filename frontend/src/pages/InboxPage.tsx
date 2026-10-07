import { Link, useParams, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import DocumentsTab from "../components/inbox/DocumentsTab";
import NotificationsTab from "../components/inbox/NotificationsTab";
import TermsTab from "../components/inbox/TermsTab";
import ThreadDetail from "../components/inbox/ThreadDetail";
import ThreadList from "../components/inbox/ThreadList";
import { useUnreadCount } from "../hooks/useInbox";
import type { Folders } from "../services/inboxService";

type Tab = Folders | "notifications" | "documents" | "terms";

const TABS: { key: Tab; labelKey: string }[] = [
    { key: "inbox", labelKey: "inbox.title" },
    { key: "notifications", labelKey: "inbox.notifications" },
    { key: "sent", labelKey: "inbox.sent" },
    { key: "archive", labelKey: "inbox.archive" },
    { key: "documents", labelKey: "inbox.documents" },
    { key: "terms", labelKey: "inbox.terms" },
];

export default function InboxPage() {
    const { t } = useTranslation();
    const params = useParams();
    const [searchParams, setSearchParams] = useSearchParams();
    const { data: unreadCount = 0 } = useUnreadCount();

    const tabParam = searchParams.get("folder");
    const tab: Tab = TABS.some((item) => item.key === tabParam) ? (tabParam as Tab) : "inbox";
    const folder: Folders | null = tab === "inbox" || tab === "sent" || tab === "archive" ? tab : null;
    const page = Math.max(1, Math.floor(Number(searchParams.get("page"))) || 1);
    const parsedId = Number(params.threadId);
    const threadId = Number.isInteger(parsedId) && parsedId > 0 ? parsedId : undefined;

    const query = searchParams.toString();
    const search = query ? `?${query}` : "";
    const detailOpen = threadId !== undefined;

    const setPage = (next: number) => {
        setSearchParams((prev) => {
            const nextParams = new URLSearchParams(prev);
            nextParams.set("page", String(next));
            return nextParams;
        });
    };

    return (
        <main className="mx-auto w-full max-w-6xl px-4 py-6 font-montserrat text-sm text-dark-navy">
            <div className="flex flex-wrap items-baseline justify-between gap-3 border-b-2 border-nordiska-orange pb-3">
                <h1 className="text-2xl font-bold">{t("inbox.messages")}</h1>
                <span className="text-xs uppercase text-primary-blue">
                    {t("inbox.unread")} <strong className="ml-1 text-base text-dark-navy">{unreadCount}</strong>
                </span>
            </div>

            <nav aria-label={t("inbox.folders-label")} className="my-4 flex flex-wrap gap-x-6 gap-y-2">
                {TABS.map((item) => (
                    <Link
                        key={item.key}
                        to={`/inbox?folder=${item.key}`}
                        aria-current={item.key === tab ? "page" : undefined}
                        className={`pb-1 font-semibold ${item.key === tab ? "border-b-[3px] border-nordiska-orange" : ""}`}
                    >
                        {t(item.labelKey)}
                    </Link>
                ))}
            </nav>

            {tab === "notifications" && <NotificationsTab page={page} onPageChange={setPage} />}
            {tab === "documents" && <DocumentsTab page={page} onPageChange={setPage} />}
            {tab === "terms" && <TermsTab />}

            {folder && (
                <div className="grid items-start gap-5 md:grid-cols-[minmax(0,420px)_minmax(0,1fr)]">
                    <div className={detailOpen ? "hidden md:block" : ""}>
                        <ThreadList
                            folder={folder}
                            page={page}
                            search={search}
                            selectedId={threadId}
                            onPageChange={setPage}
                        />
                    </div>

                    <div className={detailOpen ? "" : "hidden md:block"}>
                        {threadId !== undefined ? (
                            <>
                                <Link
                                    to={{ pathname: "/inbox", search }}
                                    className="mb-3 inline-block text-primary-blue md:hidden"
                                >
                                    <span aria-hidden="true">‹ </span>{t("inbox.back")}
                                </Link>
                                <ThreadDetail key={threadId} id={threadId} search={search} folder={folder} />
                            </>
                        ) : (
                            <p className="bg-white px-4 py-3 text-secondary shadow-card">{t("inbox.select-prompt")}</p>
                        )}
                    </div>
                </div>
            )}
        </main>
    );
}
