import { useEffect, useState, useMemo } from "react";
import { useFaqs } from "../../../hooks/useFaqs";
import { deleteFaq, getFaqsByRelationId } from "../../../services/faqService";
import { useQueryClient } from "@tanstack/react-query";
import type { Faq, FaqGroup } from "../../../types/types";
import FaqGroupList from "../../../components/admin/faq/FaqGroupList";

export default function DeleteFaq() {
    const [lang, setLang] = useState<"sv" | "en">("sv");
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [faqGroups, setFaqGroups] = useState<FaqGroup[]>([]);
    const queryClient = useQueryClient();

    const { data, isLoading, isError } = useFaqs(lang,1,search,"");
    const faqs = useMemo(() => data?.items ?? [], [data?.items]);

    // SÖK DEBOUNCE
    useEffect(() => {
        const timer = setTimeout(() => {
            setSearch(searchInput.trim());
        }, 400);

        return () => clearTimeout(timer);
    }, [searchInput]);

    // GRUPPERA FAQs på RELATION ID
    useEffect(() => {
        async function loadRelatedFaqs() {
            if (!faqs.length) {setFaqGroups([]); return;}

            // hämtar alla unika IDn
            const relationIds = [
                ...new Set(
                    faqs.map((faq) => faq.relationId)
                ),
            ];

            // Gruppera Faq på relationsId
            const groups = await Promise.all(
                relationIds.map(async (relationId) => {
                    const relatedFaqs = await getFaqsByRelationId(relationId);
                    return {
                        relationId,
                        faqs: relatedFaqs,
                    };
                })
            );

            setFaqGroups(groups);
        }

        loadRelatedFaqs();
    }, [faqs]);


    const handleDelete = async (relationId: string) => {
        const confirmed = window.confirm("Är du säker på att du vill radera denna FAQ?");

        if (!confirmed) { return; }

        try {
            const relatedFaqs: Faq[] = await getFaqsByRelationId(relationId);

            await Promise.all(relatedFaqs.map((faq: Faq) => deleteFaq(faq.id)));
            await queryClient.invalidateQueries({queryKey: ["faqs", lang, 1, search, ""],});
            alert("FAQ raderades!");

        } catch (error) {
            console.error("Kunde inte radera FAQ:", error);
            alert("Något gick fel. FAQ kunde inte raderas.");
        }
    };

    return (
    <>
        <h1 className="text-3xl font-semibold py-2.5 bg-red-600 text-white text-center">Radera</h1> 

        <section className="w-full">
            <h2 className=" block w-[60%] mx-auto text-xl font-semibold border-b mt-10 py-2 bg-black text-white text-center"> SÖK EFTER FAQ </h2>
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

            {/* FAQ KORT  */}
            <FaqGroupList 
                faqGroups={faqGroups}
                actionLabel="Radera"
                actionBtnStyle="delete"
                onAction={handleDelete}
            />
        </section>
    </>
    );
}