import { useTranslation } from "react-i18next";

type FaqCategoriesProps = {
    categories: string[];
    selectedCategory: string;
    error?: string;
    onSelect: (category: string) => void;
};

const FaqCategories = ({
    categories,
    selectedCategory,
    error,
    onSelect,
}: FaqCategoriesProps) => {
    const {t} = useTranslation();

    if (error) {
        return (
            <section className="flex flex-wrap gap-2 justify-center mt-4 max-w-5xl mx-auto">
                <p className="text-white">
                    {error}
                </p>
            </section>
        );
    }
    
    return (
        <section className="flex flex-wrap gap-2 justify-center mt-4 max-w-5xl mx-auto">
            <button
                type="button"
                onClick={() => onSelect("")}
                className={`cursor-pointer rounded-3xl border-2 px-3 py-1 text-sm hover:bg-nordiska-orange hover:text-dark-navy ${
                    selectedCategory === ""
                        ? "border-nordiska-orange bg-nordiska-orange text-dark-navy"
                        : "border-nordiska-orange text-white"
                }`}
            >
                {t("generic.all")}
            </button>

            {categories.map((category) => (
                <button
                    key={category}
                    type="button"
                    onClick={() => onSelect(category)}
                    className={`cursor-pointer rounded-3xl border-2 px-3 py-1 text-sm hover:bg-nordiska-orange hover:text-dark-navy ${
                        selectedCategory === category
                            ? "border-nordiska-orange bg-nordiska-orange text-dark-navy"
                            : "border-nordiska-orange text-white"
                    }`}
                >
                    {category}
                </button>
            ))}
        </section>
    );
};

export default FaqCategories;