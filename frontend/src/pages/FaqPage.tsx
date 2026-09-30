import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { logHelpCount } from "../services/faqService";
import { useFaqs } from "../hooks/useFaqs";
import { useFaqCategories } from "../hooks/useFaqCategories";

import FaqHeader from "../components/faq/FaqHeader";
import FaqFilterInfo from "../components/faq/FaqFilterInfo";
import FaqList from "../components/faq/FaqList";
import FaqPagination from "../components/faq/FaqPagination";
import FaqSidebar from "../components/faq/FaqSidebar";

type FeedbackAction = "increase" | "decrease";

const FaqPage = () => {
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
    const {
        data,
        isLoading,
        isError,
    } = useFaqs(
        i18n.language,
        page,
        search,
        category
    );

    // Categories
    const {
        data: categories = [],
        isError: categoriesError,
    } = useFaqCategories(i18n.language);

    // Handlers
    const handleLanguageChange = () => {
        setCategory("");
    };

    const handleCategoryChange = (
        newCategory: string
    ) => {
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
            console.error(
                "Kunde inte uppdatera helpcount",
                error
            );
        }
    };

    const handleOpenChange = (
        id: number,
        open: boolean
    ) => {
        setOpenFaqId(open ? id : null);
    };

    const faqs = data?.items ?? [];

    return (
        <main className="relative min-h-[calc(100vh-75px)] flex flex-col bg-light-gray text-dark-navy">

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

            <div className="flex-1 w-full flex flex-col lg:flex-row gap-5 p-4 sm:p-5">

                <section className="w-full lg:flex-[2] bg-white shadow-md p-4 sm:p-5">

                    <FaqFilterInfo
                        category={category}
                        searchInput={searchInput}
                    />

                    <FaqList
                        faqs={faqs}
                        isLoading={isLoading}
                        isError={isError}
                        openFaqId={openFaqId}
                        selectedFeedback={selectedFeedback}
                        onOpenChange={handleOpenChange}
                        onFeedback={handleHelpCount}
                    />

                    {data && (
                        <FaqPagination
                            page={data.page}
                            totalPages={data.totalPages}
                            hasPreviousPage={
                                data.hasPreviousPage
                            }
                            hasNextPage={
                                data.hasNextPage
                            }
                            onPrevious={() =>
                                setPage(
                                    (currentPage) =>
                                        currentPage - 1
                                )
                            }
                            onNext={() =>
                                setPage(
                                    (currentPage) =>
                                        currentPage + 1
                                )
                            }
                        />
                    )}

                </section>

                <FaqSidebar onLanguageChange={handleLanguageChange} />
            </div>
        </main>
    );
};

export default FaqPage;