import { Link, useParams, useSearchParams } from "react-router";
import DocumentsTab from "../components/inbox/DocumentsTab";
import ThreadDetail from "../components/inbox/ThreadDetail";
import ThreadList from "../components/inbox/ThreadList";
import { useUnreadCount } from "../hooks/useInbox";
import type { Folders } from "../services/inboxService";

type Tab = Folders | "documents";

const TABS: { key: Tab; label: string }[] = [
    { key: "inbox", label: "Inbox" },
    { key: "sent", label: "Sent" },
    { key: "archive", label: "Archive" },
    { key: "documents", label: "Documents" },
];

export default function InboxPage() {
    const params = useParams();
    const [searchParams, setSearchParams] = useSearchParams();
    const { data: unreadCount = 0 } = useUnreadCount();

    const tabParam = searchParams.get("folder");
    const tab: Tab = TABS.some((t) => t.key === tabParam) ? (tabParam as Tab) : "inbox";
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
                <h1 className="text-2xl font-bold">Messages</h1>
                <span className="text-xs uppercase text-primary-blue">
                    Unread <strong className="ml-1 text-base text-dark-navy">{unreadCount}</strong>
                </span>
            </div>

            <nav aria-label="Folders" className="my-4 flex flex-wrap gap-x-6 gap-y-2">
                {TABS.map((t) => (
                    <Link
                        key={t.key}
                        to={`/inbox?folder=${t.key}`}
                        aria-current={t.key === tab ? "page" : undefined}
                        className={`pb-1 font-semibold ${t.key === tab ? "border-b-[3px] border-nordiska-orange" : ""}`}
                    >
                        {t.label}
                    </Link>
                ))}
            </nav>

            {tab === "documents" ? (
                <DocumentsTab page={page} onPageChange={setPage} />
            ) : (
                <div className="grid items-start gap-5 md:grid-cols-[minmax(0,420px)_minmax(0,1fr)]">
                    <div className={detailOpen ? "hidden md:block" : ""}>
                        <ThreadList
                            folder={tab}
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
                                    <span aria-hidden="true">‹ </span>Back
                                </Link>
                                <ThreadDetail key={threadId} id={threadId} search={search} folder={tab} />
                            </>
                        ) : (
                            <p className="bg-white px-4 py-3 text-secondary shadow-card">Select a message to read it.</p>
                        )}
                    </div>
                </div>
            )}
        </main>
    );
}
