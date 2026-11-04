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
        <section className="mt-4 max-w-5xl mx-auto">

            {/* ----- MOBILE ----- */}
            <div className="md:hidden flex justify-center">
                <div className="relative w-full max-w-xs">
                    <select
                        value={selectedCategory}
                        onChange={(e) => onSelect(e.target.value)}
                        className="w-full appearance-none rounded-3xl border-2 border-nordiska-orange bg-dark-navy text-white pl-4 pr-10 py-2 text-sm"
                    >
                        <option value="">{t("generic.all")}</option>

                        {categories.map((category) => (
                            <option key={category} value={category}>
                                {category}
                            </option>
                        ))}
                    </select>

                    <svg
                        className="pointer-events-none absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-nordiska-orange"
                        viewBox="0 0 20 20"
                        fill="currentColor"
                        aria-hidden="true"
                    >
                        <path
                            fillRule="evenodd"
                            d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z"
                            clipRule="evenodd"
                        />
                    </svg>
                </div>
            </div>

            {/* ----- DESKTOP ----- */}
            <div className="hidden md:flex flex-wrap gap-2 justify-center">
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
            </div>
        </section>
    );
};

export default FaqCategories;