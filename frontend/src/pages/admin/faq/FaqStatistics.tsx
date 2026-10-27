import { useEffect, useState, useMemo } from "react";
import { useFaqs } from "../../../hooks/useFaqs";
import PagePagination from "../../../components/PagePagination";

type FaqItem = NonNullable<ReturnType<typeof useFaqs>["data"]>["items"][number];

// Sökordsstatistik: ett objekt per sökord. Justera när spårningen är byggd.
type KeywordStat = {
    term: string;
    count: number;
    existsInFaq: boolean;
};

type SortKey = "views" | "helpful";

const FAQ_PAGE_SIZE = 10; // TODO: samma som backend returnerar per sida
const KEYWORD_PAGE_SIZE = 10;
const SECTION = "w-full max-w-6xl mx-auto px-4";

// TODO: ersätt med hook, t.ex. useKeywordStats(lang)
const NO_KEYWORD_DATA: KeywordStat[] = [];

// TODO: ersätt med riktig visningsdata, t.ex. faq.viewCount ?? 0
function getViews(_faq: FaqItem): number {
    return 0;
}

// TODO: ersätt med er redigera-route (och <Link> om ni använder react-router)
function getEditHref(faq: FaqItem): string {
    return `/faq/redigera/${faq.id}`;
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

const FAQ_COLS = "grid grid-cols-[1fr_7rem_5rem_2rem] items-center gap-3 px-3 min-h-11";
const KEYWORD_COLS = "grid grid-cols-[1fr_7rem_7rem] items-center gap-3 px-3 min-h-11";

export default function FaqStatistics() {
    const [lang, setLang] = useState<"sv" | "en">("sv");
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [page, setPage] = useState(1);
    const [keywordPage, setKeywordPage] = useState(1);
    const [sortKey, setSortKey] = useState<SortKey>("views");
    const [sortOrder, setSortOrder] = useState<"asc" | "desc">("desc");

    const { data, isLoading, isError, isFetching } = useFaqs(lang, page, FAQ_PAGE_SIZE, search, "");
    const faqs = useMemo(() => data?.items ?? [], [data?.items]);
    const totalPages = data?.totalPages ?? 1;
    const hasPreviousPage = data?.hasPreviousPage ?? false;
    const hasNextPage = data?.hasNextPage ?? false;

    const keywordStats = NO_KEYWORD_DATA;

    // SORTERA PÅ VISNINGAR / BETYG
    const sortedFaqs = useMemo(() => {
        const dir = sortOrder === "asc" ? 1 : -1;
        const value = (faq: FaqItem) =>
            sortKey === "views" ? getViews(faq) : (faq.helpfulCount ?? 0);
        return faqs.toSorted((a, b) => dir * (value(a) - value(b)));
    }, [faqs, sortKey, sortOrder]);

    function toggleSort(key: SortKey) {
        if (key === sortKey) {
            setSortOrder((o) => (o === "asc" ? "desc" : "asc"));
        } else {
            setSortKey(key);
            setSortOrder("desc");
        }
    }

    function sortArrow(key: SortKey) {
        if (key !== sortKey) return "";
        return sortOrder === "desc" ? " ▼" : " ▲";
    }

    // SÖKORD: FILTRERA, SUMMERA, PAGINERA
    const filteredKeywords = useMemo(() => {
        const q = search.toLowerCase();
        return keywordStats
            .filter((k) => k.term.toLowerCase().includes(q))
            .toSorted((a, b) => b.count - a.count);
    }, [keywordStats, search]);

    const keywordTotals = useMemo(() => {
        const total = filteredKeywords.reduce((sum, k) => sum + k.count, 0);
        const missing = filteredKeywords
            .filter((k) => !k.existsInFaq)
            .reduce((sum, k) => sum + k.count, 0);
        const missingPct = total > 0 ? Math.round((missing / total) * 100) : 0;
        return { total, found: total - missing, missing, missingPct };
    }, [filteredKeywords]);

    const keywordTotalPages = Math.max(1, Math.ceil(filteredKeywords.length / KEYWORD_PAGE_SIZE));
    const pagedKeywords = filteredKeywords.slice(
        (keywordPage - 1) * KEYWORD_PAGE_SIZE,
        keywordPage * KEYWORD_PAGE_SIZE
    );

    // SÖK DEBOUNCE (+ tillbaka till sida 1)
    useEffect(() => {
        const timer = setTimeout(() => {
            setSearch(searchInput.trim());
            setPage(1);
            setKeywordPage(1);
        }, 400);

        return () => clearTimeout(timer);
    }, [searchInput]);

    function changeLang(next: "sv" | "en") {
        setLang(next);
        setPage(1);
        setKeywordPage(1);
    }

    return (
        <>
            <h1 className="text-3xl font-semibold py-2.5 bg-black text-white text-center">Statistik</h1>

            {/* SÖK */}
            <section className={SECTION}>
                <h2 className="block w-full text-xl font-semibold border-b mt-10 py-2 bg-black text-white text-center">SÖK</h2>
                <div className="flex justify-center gap-10 mt-3">
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
                <input
                    type="text"
                    aria-label="Sök"
                    placeholder="Sök på fråga, svar, kategori eller sökord"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    className="block w-full bg-white border rounded-2xl py-2.5 pl-4 pr-10 text-sm sm:text-base focus:outline-none focus-visible:ring-2 focus-visible:ring-nordiska-orange"
                />
            </section>

            {/* FAQ */}
            <section className={`${SECTION} mt-10`}>
                <h2 className="text-xl font-semibold py-2 bg-black text-white text-center">FAQ</h2>

                <div className={`${FAQ_COLS} py-2 bg-gray-200 text-xs font-semibold uppercase`}>
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

                <div className={`relative transition-opacity ${isFetching ? "opacity-50" : ""}`} style={reservedHeight(FAQ_PAGE_SIZE)}>
                    {sortedFaqs.map((faq) => {
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
                                    <p className="bg-gray-200 p-1 text-base">{faq.answer}</p>
                                    <p><strong>Kategori: </strong>{faq.category}</p>
                                    <p><strong>Keywords: </strong>{faq.keywords?.join(", ")}</p>
                                    <p><strong>ID: </strong>{faq.id}</p>
                                    <p><strong>Relations ID: </strong>{faq.relationId}</p>
                                    <a
                                        href={getEditHref(faq)}
                                        className="self-start border-4 border-black rounded px-5 py-1 font-semibold hover:bg-gray-200"
                                    >
                                        Redigera
                                    </a>
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

                {totalPages > 1 && (
                    <div className="my-3">
                        <PagePagination
                            page={page}
                            totalPages={totalPages}
                            hasPreviousPage={hasPreviousPage}
                            hasNextPage={hasNextPage}
                            onPrevious={() => setPage((p) => p - 1)}
                            onNext={() => setPage((p) => p + 1)}
                            onPageChange={setPage}
                            isDisabled={isFetching}
                        />
                    </div>
                )}
            </section>

            {/* SÖKORD */}
            <section className={`${SECTION} mt-10 mb-16`}>
                <h2 className="text-xl font-semibold py-2 bg-black text-white text-center">SÖKORD</h2>

                <p className="flex flex-wrap justify-center gap-x-8 gap-y-1 py-2 text-sm border-b border-gray-300">
                    <span>Sökningar: <strong className="tabular-nums">{keywordTotals.total}</strong></span>
                    <span>Finns i FAQ: <strong className="tabular-nums">{keywordTotals.found}</strong></span>
                    <span>
                        Saknas:{" "}
                        <strong className={`tabular-nums ${keywordTotals.missing > 0 ? "text-red-700" : ""}`}>
                            {keywordTotals.missing} ({keywordTotals.missingPct} %)
                        </strong>
                    </span>
                </p>

                <div className={`${KEYWORD_COLS} py-2 bg-gray-200 text-xs font-semibold uppercase`}>
                    <span>Sökord</span>
                    <span className="text-right">Antal</span>
                    <span className="text-right">Finns i FAQ</span>
                </div>

                <div style={reservedHeight(KEYWORD_PAGE_SIZE)}>
                    {pagedKeywords.length === 0 ? (
                        <p className="px-3 py-4 text-sm">
                            {search ? "Inga sökord matchar." : "Ingen sökdata ännu."}
                        </p>
                    ) : (
                        pagedKeywords.map((k) => (
                            <div
                                key={k.term}
                                className={`${KEYWORD_COLS} py-2 border-b border-gray-300 ${k.existsInFaq ? "" : "bg-red-50"}`}
                            >
                                <span className="font-mono text-sm">{k.term}</span>
                                <span className="text-right tabular-nums">{k.count}</span>
                                <span className={`text-right font-semibold ${k.existsInFaq ? "text-green-700" : "text-red-700"}`}>
                                    {k.existsInFaq ? "Ja" : "Nej"}
                                </span>
                            </div>
                        ))
                    )}
                </div>

                {keywordTotalPages > 1 && (
                    <div className="my-3">
                        <PagePagination
                            page={keywordPage}
                            totalPages={keywordTotalPages}
                            hasPreviousPage={keywordPage > 1}
                            hasNextPage={keywordPage < keywordTotalPages}
                            onPrevious={() => setKeywordPage((p) => p - 1)}
                            onNext={() => setKeywordPage((p) => p + 1)}
                            onPageChange={setKeywordPage}
                        />
                    </div>
                )}
            </section>
        </>
    );
}
