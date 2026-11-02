import { useEffect, useState, useMemo } from "react";
import { useFaqs } from "../../../hooks/useFaqs";
import { getFaqsByRelationId } from "../../../services/faqService";
import type { FaqGroup } from "../../../types/types";
import { useNavigate, useLocation } from "react-router";
import FaqGroupList from "../../../components/admin/faq/FaqGroupList";
import PagePagination from "../../../components/PagePagination";
import AdminStatus from "../../../components/admin/ui/AdminStatus";
import { AdminPageTitle, AdminSectionTitle } from "../../../components/admin/ui/AdminTitle";

export default function EditFaq() {
    const [lang, setLang] = useState<"sv" | "en">("sv");
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [page, setPage] = useState(1);
    const [faqGroups, setFaqGroups] = useState<FaqGroup[]>([]);
    const navigate = useNavigate();
    const location = useLocation();
    const feedback = location.state?.feedback;
    const PAGE_SIZE = 5;

    const { data, isLoading, isError } = useFaqs(lang, page, PAGE_SIZE, search, "");
    const faqs = useMemo(() => data?.items ?? [], [data?.items]);
    const totalPages = data?.totalPages ?? 0;

    // VISA UPDATE SUCCESS FEEDBACK
    useEffect(() => {
        if (!feedback) return;

        const timer = setTimeout(() => {
            navigate(location.pathname, {
                replace: true,
                state: null,
            });
        }, 4000);

        return () => clearTimeout(timer);
    }, [feedback, navigate, location.pathname]);

    // SÖK DEBOUNCE
    useEffect(() => {
        const timer = setTimeout(() => {
            setSearch(searchInput.trim());
            setPage(1);
        }, 400);

        return () => clearTimeout(timer);
    }, [searchInput]);

    // GRUPPERA FAQs på RELATION ID
    useEffect(() => {
        async function loadRelatedFaqs() {
            if (!faqs.length) { setFaqGroups([]); return; }

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

    // REDIRECTA TILL update form vid Edit-action
    const handleEdit = async (relationId: string) => {
        try {
            const relatedFaqs = await getFaqsByRelationId(relationId);
            navigate("/admin/faq/edit/form", {
                state: {
                    faqGroup: {
                        relationId,
                        faqs: relatedFaqs,
                    },
                },
            });
        } catch (error) {
            console.error("Kunde inte hämta FAQ:", error);
            alert("Något gick fel. FAQ kunde inte öppnas.");
        }
    };

    return (
        <>
            <AdminPageTitle>Redigera</AdminPageTitle>

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
                            onChange={() => { setLang("sv"); setPage(1); }}
                        />
                        <span className="ml-2">Svenska</span>
                    </label>
                    <label>
                        <input
                            type="radio"
                            name="language"
                            value="en"
                            checked={lang === "en"}
                            onChange={() => { setLang("en"); setPage(1); }}
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

                {/* Uppdaterd FAQ message */}
                {feedback && (
                    <div className="block w-[60%] mx-auto mt-4 border-5 bg-green-500 border-black rounded-md px-4 py-2 font-semibold">
                        <p className="text-center">{feedback}</p>
                    </div>
                )}

                {isLoading && search && <AdminStatus kind="loading">Söker...</AdminStatus>}
                {isError && <AdminStatus kind="error">Kunde inte hämta FAQ.</AdminStatus>}
                {!isLoading && search && faqs.length === 0 && <AdminStatus kind="empty">Inga FAQ hittades.</AdminStatus>}

                {/* FAQ KORT  */}
                <FaqGroupList
                    faqGroups={faqGroups}
                    actionLabel="Redigera"
                    actionBtnStyle="edit"
                    onAction={handleEdit}
                />

                {/* PAGINERING */}
                <div className="my-3">
                    <PagePagination
                        page={page}
                        totalPages={totalPages}
                        hasPreviousPage={page > 1}
                        hasNextPage={page < totalPages}
                        onPrevious={() => setPage(page - 1)}
                        onNext={() => setPage(page + 1)}
                        onPageChange={setPage}
                        isDisabled={isLoading}
                    />
                </div>
            </section>
        </>
    );
}
