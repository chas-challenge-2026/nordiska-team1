import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { logHelpCount } from "../services/faqService";
import { useFaqs } from "../hooks/useFaqs";
import { useFaqCategories } from "../hooks/useFaqCategories";
import FaqHeader from "../components/faq/FaqHeader";
import FaqFilterInfo from "../components/faq/FaqFilterInfo";
import FaqList from "../components/faq/FaqList";
import FaqSidebar from "../components/faq/FaqSidebar";
import PagePagination from "../components/PagePagination";
import FaqContact from "../components/faq/FaqContact";

type FeedbackAction = "increase" | "decrease";

export default function FaqPage() {
    const { i18n,t } = useTranslation();

    // Search & filter state
    const [page, setPage] = useState(1);
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [category, setCategory] = useState("");

    // FAQ state
    const [openFaqId, setOpenFaqId] =
        useState<number | null>(null);

    const [selectedFeedback, setSelectedFeedback] =
        useState<Record<number, FeedbackAction>>({});

    // Search debounce
    useEffect(() => {
        const timer = setTimeout(() => {
            setSearch(searchInput.trim());
            setPage(1);
        }, 400);

        return () => clearTimeout(timer);
    }, [searchInput]);

    // FAQ data
    const {data, isLoading, isError,} = useFaqs(i18n.language, page, search, category);

    // Categories
    const {data: categories = [], isError: categoriesError,} = useFaqCategories(i18n.language);

    // Handlers
    const handleLanguageChange = () => {setCategory("");};

    const handleCategoryChange = (newCategory: string) => {
        setCategory(newCategory);
        setPage(1);
        setOpenFaqId(null);
    };

    const handleHelpCount = async (
        id: number,
        action: FeedbackAction
    ) => {
        try {
            await logHelpCount(id, action);

            setSelectedFeedback((current) => ({
                ...current,
                [id]: action,
            }));
        } catch (error) {
            console.error("Kunde inte uppdatera helpcount",error);
        }
    };

    const handleOpenChange = (
        id: number,
        open: boolean
    ) => {
        setOpenFaqId(open ? id : null);
    };

    const faqs = data?.items ?? [];
    const effectiveOpenFaqId = faqs.length === 1 ? faqs[0].id : openFaqId;

    return (
    <main className="relative flex min-h-0 flex-1 flex-col overflow-scroll bg-light-gray text-dark-navy">
        <FaqHeader
            searchInput={searchInput}
            category={category}
            categories={categories}
            categoriesError={
                categoriesError
                    ? t("faq-page.categories-error")
                    : undefined
            }
            onSearchChange={setSearchInput}
            onCategoryChange={handleCategoryChange}
        />
  
        <div className="sm:grid min-h-0 flex flex-col flex-1 sm:grid-cols-1 sm:grid-rows-[1fr_7%] gap-4 overflow-scroll px-3 py-4 sm:px-5 lg:grid-cols-[2fr_1fr] lg:gap-5">
            <section className="flex min-w-0 flex-2 flex-col bg-white p-4 shadow-md sm:p-5 ">
                <FaqFilterInfo
                    category={category}
                    searchInput={searchInput}
                />

                <div className="lg:flex-1 lg:overflow-y-auto">
                    <FaqList
                        faqs={faqs}
                        search={search}
                        isLoading={isLoading}
                        isError={isError}
                        openFaqId={effectiveOpenFaqId}
                        selectedFeedback={selectedFeedback}
                        onOpenChange={handleOpenChange}
                        onFeedback={handleHelpCount}
                        onSearchSuggestion={(suggestion) => {
                            setSearchInput(suggestion);
                            setSearch(suggestion);
                            setPage(1);
                        }}
                    />
                </div>

                {data && (
                    <div className="mt-5 shrink-0 overflow-x-auto pb-1">
                        <PagePagination
                            page={data.page}
                            totalPages={data.totalPages}
                            hasPreviousPage={data.hasPreviousPage}
                            hasNextPage={data.hasNextPage}
                            onPrevious={() =>
                                setPage((currentPage) => currentPage - 1)
                            }
                            onNext={() =>
                                setPage((currentPage) => currentPage + 1)
                            }
                            onPageChange={setPage}
                        />
                    </div>
                )}
            </section>

            <FaqSidebar onLanguageChange={handleLanguageChange} />
            <FaqContact />
        </div>
    </main>
    );
};
