import { useTranslation } from "react-i18next";

type FaqPaginationProps = {
    page: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    onPrevious: () => void;
    onNext: () => void;
};

const FaqPagination = ({
    page,
    totalPages,
    hasPreviousPage,
    hasNextPage,
    onPrevious,
    onNext,
}: FaqPaginationProps) => {
    const {t} = useTranslation();
    if (totalPages <= 1) {
        return null;
    }

    return (
        <div className="mt-8 flex items-center justify-center gap-4 sm:gap-6">
            <button
                type="button"
                disabled={!hasPreviousPage}
                onClick={onPrevious}
                aria-label={t("aria-label.previous-page")}
                className="cursor-pointer disabled:cursor-not-allowed disabled:opacity-40"
            >
                {t("generic.previous")}
            </button>

            <span>
                {t("generic.page")} {page} {t("generic.of")} {totalPages}
            </span>

            <button
                type="button"
                disabled={!hasNextPage}
                onClick={onNext}
                aria-label={t("aria-label.next-page")}
                className="cursor-pointer disabled:cursor-not-allowed disabled:opacity-40"
            >
                {t("generic.next")}

            </button>
        </div>
    );
};

export default FaqPagination;