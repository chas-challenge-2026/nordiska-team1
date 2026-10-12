import FaqSearch from "./FaqSearch";
import FaqCategories from "./FaqCategories";
import { useTranslation } from "react-i18next";
import {motion} from "motion/react";

type FaqHeaderProps = {
    searchInput: string;
    category: string;
    categories: string[];
    categoriesError?: string;
    onSearchChange: (value: string) => void;
    onCategoryChange: (category: string) => void;
};

export default function FaqHeader({
    searchInput,
    category,
    categories,
    categoriesError,
    onSearchChange,
    onCategoryChange,
}: FaqHeaderProps) {

    const {t} = useTranslation();
    return (
        <motion.header
            key="faqHeader"
            initial= {{y: "-100%"}}
            animate= {{y:0}}
            transition={{
                    duration: 0.3,
                    ease: "easeOut",
                }}
            className="bg-dark-navy pb-3 sm:pb-4 sticky top-[75px]"
        >
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
        </motion.header>
    );
};