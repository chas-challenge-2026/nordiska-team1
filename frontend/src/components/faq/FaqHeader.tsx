import FaqSearch from "./FaqSearch";
import FaqCategories from "./FaqCategories";
import { useTranslation } from "react-i18next";

type FaqHeaderProps = {
    searchInput: string;
    category: string;
    categories: string[];
    categoriesError?: string;
    onSearchChange: (value: string) => void;
    onCategoryChange: (category: string) => void;
};

const FaqHeader = ({
    searchInput,
    category,
    categories,
    categoriesError,
    onSearchChange,
    onCategoryChange,
}: FaqHeaderProps) => {

    const {t} = useTranslation();
    return (
        <header className="bg-dark-navy px-4 pt-8 pb-5 sm:pt-10 sm:pb-6">
            <h1 className="text-white text-2xl sm:text-4xl font-montserrat-alternates text-center font-semibold">
                {t("faq.faq-header.title")}
            </h1>

            <p className="text-white font-montserrat-alternates text-sm text-center mt-2">
                {t("faq.faq-header.paragraph")}
            </p>

            <FaqSearch
                value={searchInput}
                category={category}
                onChange={onSearchChange}
            />

            <FaqCategories
                categories={categories}
                selectedCategory={category}
                error={categoriesError}
                onSelect={onCategoryChange}
            />
        </header>
    );
};

export default FaqHeader;