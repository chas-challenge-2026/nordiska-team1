import { useEffect, useMemo, useRef, useState } from "react";
import { useFaqs, useFaqAnalyticsSummary, useFaqContentGaps } from "../../../hooks/useFaqs";
import { useNavigate } from "react-router";
import { getFaqsByRelationId } from "../../../services/faqService";
import PagePagination from "../../../components/PagePagination";
import AdminButton from "../../../components/admin/ui/AdminButton";
import { AdminPageTitle } from "../../../components/admin/ui/AdminTitle";
type FaqItem = NonNullable<ReturnType<typeof useFaqs>["data"]>["items"][number];
type SortKey = "views" | "helpful";

const DAYS = 30;
const FAQ_PAGE_SIZE = 10;
const VIEWED_PAGE_SIZE = 10;
const GAPS_PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 400;
const SECTION = "w-full max-w-6xl mx-auto px-4";

// Värden som skickas som sortBy till backend (tillåtna: id, helpful, views)
const SORT_FIELD: Record<SortKey, "id" | "helpful" | "views"> = {
    views: "views",
    helpful: "helpful",
};

const FAQ_COLS = "grid grid-cols-[1fr_7rem_5rem_2rem] items-center gap-3 px-3 min-h-11";
const VIEWED_COLS = "grid grid-cols-[1fr_10rem_6rem] items-center gap-3 px-3 min-h-11";
const GAP_COLS = "grid grid-cols-[1fr_6rem_8rem] items-center gap-3 px-3 min-h-11";
const TABLE_HEAD = "py-2 bg-gray-200 text-xs font-semibold uppercase";
const SECTION_TITLE = "text-xl font-semibold py-2 bg-black text-white text-center";

function getViews(faq: FaqItem): number {
    return faq.viewCount ?? 0;
}

function formatScore(score: number) {
    return score > 0 ? `+${score}` : `${score}`;
}

function scoreClass(score: number) {
    if (score > 0) return "text-green-700";
    if (score < 0) return "text-red-700";
    return "";
}

// En rad = min-h-11 (2.75rem) + 1px border-b
function reservedHeight(rows: number) {
    return { minHeight: `calc(${rows} * (2.75rem + 1px))` };
}

function paginate<T>(items: T[], page: number, size: number): T[] {
    return items.slice((page - 1) * size, page * size);
}

function totalPagesFor(count: number, size: number) {
    return Math.max(1, Math.ceil(count / size));
}

function rangeText(page: number, size: number, total: number) {
    if (total === 0) return "Visar 0 av 0";
    const from = (page - 1) * size + 1;
    const to = Math.min(page * size, total);
    return `Visar ${from}–${to} av ${total}`;
}

export default function FaqStatistics() {
    const navigate = useNavigate();

    // Öppnar redigeringsformuläret med båda språkversionerna (samma flöde som EditFaq)
    async function openEdit(relationId: string) {
        try {
            const faqs = await getFaqsByRelationId(relationId);
            navigate("/admin/faq/edit/form", { state: { faqGroup: { relationId, faqs } } });
        } catch (error) {
            console.error("Kunde inte hämta FAQ:", error);
            alert("Något gick fel. FAQ kunde inte öppnas.");
        }
    }

    const [lang, setLang] = useState<"sv" | "en">("sv");

    // Alla FAQ: sök (debounce, filtreras i backend)
    const [faqSearchInput, setFaqSearchInput] = useState("");
    const [faqSearch, setFaqSearch] = useState("");
    const searchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

    // Sökningar utan träff: filter (i frontend)
    const [gapsFilter, setGapsFilter] = useState("");

    const [page, setPage] = useState(1);
    const [viewedPage, setViewedPage] = useState(1);
    const [gapsPage, setGapsPage] = useState(1);
    const [sortKey, setSortKey] = useState<SortKey>("helpful");
    const [sortOrder, setSortOrder] = useState<"asc" | "desc">("desc");

    // FAQ-LISTA (sortering och paginering i backend)
    const { data, isLoading, isError, isFetching } = useFaqs(
        lang,
        page,
        FAQ_PAGE_SIZE,
        faqSearch,
        undefined,
        undefined,
        SORT_FIELD[sortKey],
        sortOrder
    );
    const faqs = useMemo(() => data?.items ?? [], [data?.items]);
    const totalPages = data?.totalPages ?? 1;
    const totalCount = data?.totalCount ?? 0;

    // STATISTIK
    const { data: summary, isError: isSummaryError } = useFaqAnalyticsSummary({ lang, days: DAYS, limit: 50 });
    const { data: contentGaps, isError: isGapsError } = useFaqContentGaps({ lang, days: DAYS, limit: 200 });

    const totalViews = summary?.views.totalViews ?? 0;
    const totalSearches = summary?.searches.totalSearches ?? 0;
    const successfulSearches = summary?.searches.successfulSearches ?? 0;
    const noResultSearches = summary?.searches.noResultSearches ?? 0;
    const noResultPct = totalSearches > 0 ? Math.round((noResultSearches / totalSearches) * 100) : 0;

    const mostViewed = useMemo(() => summary?.views.mostViewedArticles ?? [], [summary]);

    const gaps = useMemo(() => {
        const q = gapsFilter.trim().toLowerCase();
        return (contentGaps ?? []).filter((g) => (g.query ?? "").toLowerCase().includes(q));
    }, [contentGaps, gapsFilter]);

    const viewedTotalPages = totalPagesFor(mostViewed.length, VIEWED_PAGE_SIZE);
    const gapsTotalPages = totalPagesFor(gaps.length, GAPS_PAGE_SIZE);
    const pagedViewed = paginate(mostViewed, viewedPage, VIEWED_PAGE_SIZE);
    const pagedGaps = paginate(gaps, gapsPage, GAPS_PAGE_SIZE);

    // Rensa väntande sök-timer när sidan lämnas (ingen state sätts här)
    useEffect(() => {
        return () => clearTimeout(searchTimer.current);
    }, []);

    // SÖK DEBOUNCE i event handler (+ tillbaka till sida 1)
    function handleFaqSearchChange(value: string) {
        setFaqSearchInput(value);
        clearTimeout(searchTimer.current);
        searchTimer.current = setTimeout(() => {
            setFaqSearch(value.trim());
            setPage(1);
        }, SEARCH_DEBOUNCE_MS);
    }

    function handleGapsFilterChange(value: string) {
        setGapsFilter(value);
        setGapsPage(1);
    }

    function changeLang(next: "sv" | "en") {
        setLang(next);
        setPage(1);
        setViewedPage(1);
        setGapsPage(1);
    }

    function toggleSort(key: SortKey) {
        if (key === sortKey) {
            setSortOrder((o) => (o === "asc" ? "desc" : "asc"));
        } else {
            setSortKey(key);
            setSortOrder("desc");
        }
        setPage(1);
    }

    function sortArrow(key: SortKey) {
        if (key !== sortKey) return "";
        return sortOrder === "desc" ? " ▼" : " ▲";
    }

    return (
        <>
            <AdminPageTitle>Statistik</AdminPageTitle>

            {/* SPRÅK (gäller hela sidan) */}
            <div className={`${SECTION} flex justify-center gap-10 mt-4`}>
                <label>
                    <input
                        type="radio"
                        name="language"
                        value="sv"
                        checked={lang === "sv"}
                        onChange={() => changeLang("sv")}
                    />
                    <span className="ml-2">Svenska</span>
                </label>
                <label>
                    <input
                        type="radio"
                        name="language"
                        value="en"
                        checked={lang === "en"}
                        onChange={() => changeLang("en")}
                    />
                    <span className="ml-2">Engelska</span>
                </label>
            </div>

            {/* ÖVERSIKT */}
            <section className={`${SECTION} mt-6`}>
                <h2 className={SECTION_TITLE}>ÖVERSIKT – SENASTE {DAYS} DAGARNA</h2>
                {isSummaryError && <p className="px-3 py-2 text-sm">Kunde inte hämta statistik.</p>}
                <p className="flex flex-wrap justify-center gap-x-8 gap-y-1 py-3 border-b border-gray-300">
                    <span>Visningar: <strong className="tabular-nums">{totalViews}</strong></span>
                    <span>Sökningar: <strong className="tabular-nums">{totalSearches}</strong></span>
                    <span>Med träff: <strong className="tabular-nums">{successfulSearches}</strong></span>
                    <span>
                        Utan träff:{" "}
                        <strong className={`tabular-nums ${noResultSearches > 0 ? "text-red-700" : ""}`}>
                            {noResultSearches} ({noResultPct} %)
                        </strong>
                    </span>
                </p>
            </section>

            {/* MEST VISADE */}
            <section className={`${SECTION} mt-10`}>
                <h2 className={SECTION_TITLE}>MEST VISADE</h2>
                <div className={`${VIEWED_COLS} ${TABLE_HEAD}`}>
                    <span>Fråga</span>
                    <span>Kategori</span>
                    <span className="text-right">Visningar</span>
                </div>

                <div style={reservedHeight(VIEWED_PAGE_SIZE)}>
                    {pagedViewed.length === 0 ? (
                        <p className="px-3 py-4 text-sm">Inga visningar ännu.</p>
                    ) : (
                        pagedViewed.map((a) => (
                            <div key={a.id} className={`${VIEWED_COLS} py-2 border-b border-gray-300`}>
                                <span className="font-semibold">{a.question ?? "–"}</span>
                                <span className="text-sm">{a.category ?? "–"}</span>
                                <span className="text-right tabular-nums">{a.viewCount}</span>
                            </div>
                        ))
                    )}
                </div>

                <div className="my-3 flex flex-col items-center gap-2">
                    <p className="text-sm text-gray-600 tabular-nums">
                        {rangeText(viewedPage, VIEWED_PAGE_SIZE, mostViewed.length)}
                    </p>
                    {viewedTotalPages > 1 && (
                        <PagePagination
                            page={viewedPage}
                            totalPages={viewedTotalPages}
                            hasPreviousPage={viewedPage > 1}
                            hasNextPage={viewedPage < viewedTotalPages}
                            onPrevious={() => setViewedPage((p) => p - 1)}
                            onNext={() => setViewedPage((p) => p + 1)}
                            onPageChange={setViewedPage}
                        />
                    )}
                </div>
            </section>

            {/* SÖKNINGAR UTAN TRÄFF */}
            <section className={`${SECTION} mt-10`}>
                <h2 className={SECTION_TITLE}>SÖKNINGAR UTAN TRÄFF</h2>
                <input
                    type="text"
                    aria-label="Filtrera sökord"
                    placeholder="Filtrera sökord"
                    value={gapsFilter}
                    onChange={(e) => handleGapsFilterChange(e.target.value)}
                    className="block w-full max-w-sm ml-auto bg-white border rounded-2xl my-3 py-1.5 pl-4 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-nordiska-orange"
                />
                <div className={`${GAP_COLS} ${TABLE_HEAD}`}>
                    <span>Sökord</span>
                    <span className="text-right">Antal</span>
                    <span className="text-right">Senast sökt</span>
                </div>

                <div style={reservedHeight(GAPS_PAGE_SIZE)}>
                    {isGapsError ? (
                        <p className="px-3 py-4 text-sm">Kunde inte hämta sökningar.</p>
                    ) : pagedGaps.length === 0 ? (
                        <p className="px-3 py-4 text-sm">
                            {gapsFilter ? "Inga sökord matchar." : "Inga sökningar utan träff."}
                        </p>
                    ) : (
                        pagedGaps.map((g) => (
                            <div key={`${g.query}-${g.lang}`} className={`${GAP_COLS} py-2 border-b border-gray-300`}>
                                <span className="font-mono text-sm">{g.query ?? "–"}</span>
                                <span className="text-right tabular-nums">{g.searchCount}</span>
                                <span className="text-right text-sm tabular-nums">
                                    {new Date(g.lastSearchedAt).toLocaleDateString("sv-SE")}
                                </span>
                            </div>
                        ))
                    )}
                </div>

                <div className="my-3 flex flex-col items-center gap-2">
                    <p className="text-sm text-gray-600 tabular-nums">
                        {rangeText(gapsPage, GAPS_PAGE_SIZE, gaps.length)}
                    </p>
                    {gapsTotalPages > 1 && (
                        <PagePagination
                            page={gapsPage}
                            totalPages={gapsTotalPages}
                            hasPreviousPage={gapsPage > 1}
                            hasNextPage={gapsPage < gapsTotalPages}
                            onPrevious={() => setGapsPage((p) => p - 1)}
                            onNext={() => setGapsPage((p) => p + 1)}
                            onPageChange={setGapsPage}
                        />
                    )}
                </div>
            </section>

            {/* ALLA FAQ */}
            <section className={`${SECTION} mt-10 mb-16`}>
                <h2 className={SECTION_TITLE}>ALLA FAQ</h2>
                <input
                    type="text"
                    aria-label="Sök i alla FAQ"
                    placeholder="Sök på fråga, svar, kategori eller keywords"
                    value={faqSearchInput}
                    onChange={(e) => handleFaqSearchChange(e.target.value)}
                    className="block w-full bg-white border rounded-2xl my-3 py-2.5 pl-4 pr-10 text-sm sm:text-base focus:outline-none focus-visible:ring-2 focus-visible:ring-nordiska-orange"
                />

                <div className={`${FAQ_COLS} ${TABLE_HEAD}`}>
                    <span>Fråga</span>
                    <button
                        type="button"
                        onClick={() => toggleSort("views")}
                        className="text-right uppercase cursor-pointer hover:underline"
                    >
                        Visningar{sortArrow("views")}
                    </button>
                    <button
                        type="button"
                        onClick={() => toggleSort("helpful")}
                        className="text-right uppercase cursor-pointer hover:underline"
                    >
                        Betyg{sortArrow("helpful")}
                    </button>
                    <span aria-hidden="true" />
                </div>

                <div
                    className={`relative transition-opacity ${isFetching ? "opacity-50" : ""}`}
                    style={reservedHeight(FAQ_PAGE_SIZE)}
                >
                    {faqs.map((faq) => {
                        const score = faq.helpfulCount ?? 0;
                        return (
                            <details key={faq.id} className="group border-b border-gray-300">
                                <summary
                                    className={`${FAQ_COLS} py-2 cursor-pointer list-none [&::-webkit-details-marker]:hidden hover:bg-gray-100 ${score < 0 ? "bg-red-50" : ""}`}
                                >
                                    <span className="font-semibold">{faq.question}</span>
                                    <span className="text-right tabular-nums">{getViews(faq)}</span>
                                    <span className={`text-right font-semibold tabular-nums ${scoreClass(score)}`}>
                                        {formatScore(score)}
                                    </span>
                                    {/* TODO: byt mot SVG */}
                                    <span aria-hidden="true" className="text-center transition-transform group-open:rotate-90">▸</span>
                                </summary>

                                <div className="px-3 pb-4 flex flex-col gap-2 text-sm">
                                    <p className="bg-gray-200 p-1 text-base whitespace-pre-line">{faq.answer}</p>
                                    <p><strong>Kategori: </strong>{faq.category}</p>
                                    <p><strong>Keywords: </strong>{faq.keywords?.join(", ")}</p>
                                    <p><strong>ID: </strong>{faq.id}</p>
                                    <p><strong>Relations ID: </strong>{faq.relationId}</p>
                                    <AdminButton
                                        variant="yellow"
                                        onClick={() => openEdit(faq.relationId)}
                                        className="self-start !border-2 !px-3 !py-0.5 text-sm"
                                    >
                                        Redigera
                                    </AdminButton>
                                </div>
                            </details>
                        );
                    })}

                    {isLoading && <p className="absolute inset-x-0 top-0 px-3 py-4 text-sm">Laddar...</p>}
                    {isError && <p className="px-3 py-4 text-sm">Kunde inte hämta FAQ.</p>}
                    {!isLoading && !isError && faqs.length === 0 && (
                        <p className="px-3 py-4 text-sm">Inga FAQ hittades.</p>
                    )}
                </div>

                <div className="my-3 flex flex-col items-center gap-2">
                    <p className="text-sm text-gray-600 tabular-nums">
                        {rangeText(page, FAQ_PAGE_SIZE, totalCount)}
                    </p>
                    {totalPages > 1 && (
                        <PagePagination
                            page={page}
                            totalPages={totalPages}
                            hasPreviousPage={data?.hasPreviousPage ?? false}
                            hasNextPage={data?.hasNextPage ?? false}
                            onPrevious={() => setPage((p) => p - 1)}
                            onNext={() => setPage((p) => p + 1)}
                            onPageChange={setPage}
                            isDisabled={isFetching}
                        />
                    )}
                </div>
            </section>
        </>
    );
}
