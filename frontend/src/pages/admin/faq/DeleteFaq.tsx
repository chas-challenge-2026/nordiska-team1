import { useEffect, useState, useMemo } from "react";
import { useFaqs } from "../../../hooks/useFaqs";
import { deleteFaq, getFaqsByRelationId } from "../../../services/faqService";
import { useQueryClient } from "@tanstack/react-query";
import type { Faq, FaqGroup } from "../../../types/types";
import FaqGroupList from "../../../components/admin/faq/FaqGroupList";
import AdminStatus from "../../../components/admin/ui/AdminStatus";
import { AdminPageTitle, AdminSectionTitle } from "../../../components/admin/ui/AdminTitle";

export default function DeleteFaq() {
    const [lang, setLang] = useState<"sv" | "en">("sv");
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [faqGroups, setFaqGroups] = useState<FaqGroup[]>([]);
    const queryClient = useQueryClient();

    const { data, isLoading, isError } = useFaqs(lang,1,5,search,"");
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
        <AdminPageTitle danger>Radera</AdminPageTitle>

        <section className="w-full">
            <AdminSectionTitle className="block w-[60%] mx-auto mt-10">SÖK EFTER FAQ</AdminSectionTitle>
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

            {isLoading && search && <AdminStatus kind="loading">Söker...</AdminStatus>}
            {isError && <AdminStatus kind="error">Kunde inte hämta FAQ.</AdminStatus>}
            {!isLoading && search && faqs.length === 0 && <AdminStatus kind="empty">Inga FAQ hittades.</AdminStatus>}

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
