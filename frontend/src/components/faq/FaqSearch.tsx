import { useTranslation } from "react-i18next";

type FaqSearchProps = {
    value: string;
    category: string;
    onChange: (value: string) => void;
};

const FaqSearch = ({
    value,
    category,
    onChange,
}: FaqSearchProps) => {

    const {t} = useTranslation();

    return (
        <section className="relative w-full max-w-2xl mx-auto mt-4">
            <input
                type="text"
                placeholder={
                    category
                        ? `${t("faq.faq-search.placeholder-category")} ${category}`
                        : `${t("faq.faq-search.placeholder-no-category")}`
                }
                value={value}
                onChange={(e) => onChange(e.target.value)}
                className="w-full bg-white rounded-2xl py-2.5 pl-4 pr-10 text-sm sm:text-base focus:outline-none focus-visible:ring-2 focus-visible:ring-nordiska-orange"
            />

            {value && (
                <button
                    type="button"
                    onClick={() => onChange("")}
                    className="absolute right-3 top-1/2 -translate-y-1/2 cursor-pointer text-primary-blue text-2xl leading-none hover:text-dark-navy focus:outline-none focus-visible:ring-2 focus-visible:ring-nordiska-orange rounded"
                    aria-label={t("faq.faq-search.aria-label")}
                >
                    ×
                </button>
            )}
        </section>
    );
};

export default FaqSearch;