import { useTranslation } from "react-i18next";

type FaqFilterInfoProps = {
    category: string;
    searchInput: string;
};

const FaqFilterInfo = ({
    category,
    searchInput,
}: FaqFilterInfoProps) => {

    const {t} = useTranslation();

    return (
        <div className="border-b border-nordiska-orange text-sm pb-3 mb-4">
            {!category && !searchInput ? (
                <p>
                    {t("faq.faq-filter-info.no-filter")}
                </p>
            ) : category && !searchInput ? (
                <p>
                    {t("faq.faq-filter-info.category-filter")}{" "}
                    <span className="font-semibold">
                        {category}
                    </span>
                </p>
            ) : !category && searchInput ? (
                <p>
                    {t("faq.faq-filter-info.search-filter")}{" "}
                    <span className="font-semibold">
                        {searchInput}
                    </span>
                </p>
            ) : (
                <p>
                    {t("faq.faq-filter-info.search-filter")}{" "}
                    <span className="font-semibold">
                        {searchInput}
                    </span>{" "}
                    {t("faq.faq-filter-info.in-category")}{" "}
                    <span className="font-semibold">
                        {category}
                    </span>
                </p>
            )}
        </div>
    );
};

export default FaqFilterInfo;