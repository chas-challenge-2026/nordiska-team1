import { useEffect, useState, useMemo } from "react";
import { useFaqs } from "../../../hooks/useFaqs";

export default function FaqStatistics() {
    const [lang, setLang] = useState<"sv" | "en">("sv");
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [helpfulOrder, setHelpfulOrder] = useState<"asc" | "desc">("desc");

    const { data, isLoading, isError } = useFaqs(lang, 1, search, "");
    const faqs = useMemo(() => data?.items ?? [], [data?.items]);

    // SORTERA PÅ BETYG
    const sortedFaqs = useMemo(() => {
        const dir = helpfulOrder === "asc" ? 1 : -1;
        return faqs.toSorted(
            (a, b) => dir * ((a.helpfulCount ?? 0) - (b.helpfulCount ?? 0))
        );
    }, [faqs, helpfulOrder]);

    // SÖK DEBOUNCE
    useEffect(() => {
        const timer = setTimeout(() => {
            setSearch(searchInput.trim());
        }, 400);

        return () => clearTimeout(timer);
    }, [searchInput]);

    return (
        <>
            <h1 className="text-3xl font-semibold py-2.5 bg-black text-white text-center">Statistik</h1>

            <section className="w-full">
                <h2 className="block w-[60%] mx-auto text-xl font-semibold border-b mt-10 py-2 bg-black text-white text-center">SÖK EFTER FAQ</h2>
                {/* Språkval - radio buttons */}
                <div className="flex justify-center gap-10 mt-3">
                    <label>
                        <input
                            type="radio"
                            name="language"
                            value="sv"
                            checked={lang === "sv"}
                            onChange={() => setLang("sv")}
                        />
                        <span className="ml-2">Svenska</span>
                    </label>
                    <label>
                        <input
                            type="radio"
                            name="language"
                            value="en"
                            checked={lang === "en"}
                            onChange={() => setLang("en")}
                        />
                        <span className="ml-2">Engelska</span>
                    </label>
                </div>
                {/* Fritext sök */}
                <input
                    type="text"
                    placeholder="Sök efter FAQ via fråga, svar, kategori, keywords eller fritext"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    className="block w-[60%] mx-auto bg-white border rounded-2xl py-2.5 pl-4 pr-10 text-sm sm:text-base focus:outline-none focus-visible:ring-2 focus-visible:ring-nordiska-orange"
                />

                {isLoading && search && <p>Söker...</p>}
                {isError && <p>Kunde inte hämta FAQ.</p>}
                {!isLoading && search && faqs.length === 0 && (<p>Inga FAQ hittades.</p>)}

                {/* Sortering */}
                <div className="w-[60%] mx-auto mt-6 flex justify-end">
                    <button
                        type="button"
                        onClick={() => setHelpfulOrder((o) => (o === "asc" ? "desc" : "asc"))}
                        className="border border-black rounded-2xl px-3 py-1 text-sm hover:bg-gray-200"
                    >
                        Sortering: {helpfulOrder === "desc" ? "Högst betyg först" : "Lägst betyg först"}
                    </button>
                </div>

                {/* FAQ KORT */}
                <div className="w-[60%] mx-auto mt-4 flex flex-col gap-6">
                    {sortedFaqs.map((faq) => (
                        <div key={faq.id}>
                            {/* Relations ID */}
                            <p className="text-sm"><strong>Relations ID: </strong>{faq.relationId}</p>
                            {/* Fråga + Betyg */}
                            <div className="flex justify-between gap-2 bg-black text-white p-1 mt-1">
                                <h3 className="font-semibold">{faq.question}</h3>
                                <span className="font-semibold">Betyg: {faq.helpfulCount}</span>
                            </div>
                            {/* Svar */}
                            <p className="bg-gray-200 p-1">{faq.answer}</p>
                            {/* Kategori */}
                            <p className="mt-2 text-sm"><strong>Kategori: </strong>{faq.category}</p>
                            {/* Keywords */}
                            <p className="mt-2 text-sm"><strong>Keywords: </strong>{faq.keywords?.join(", ")}</p>
                            {/* Faq ID */}
                            <p className="mt-2 text-sm"><strong>ID: </strong>{faq.id}</p>
                        </div>
                    ))}
                </div>
            </section>
        </>
    );
}
