import { useTranslation } from "react-i18next";

/**
 * PagePagination
 *
 * Reusable pagination component for navigating through paginated data.
 * - The component supports both navigation using Previous/Next and 
 * direct navigation by selecting a specific page number.
 * 
 * Displays:
 * - Previous and Next buttons
 * - Individual page buttons
 * - Ellipsis (...) when there are many pages
 *      Example: Previous 1 ... 9 10 11 ... 20 Next
 *
 * Props:
 * - page: currently active page.
 * - totalPages: total number of available pages.
 * - hasPreviousPage: determines whether the Previous button should be enabled.
 * - hasNextPage: determines whether the Next button should be enabled.
 * - onPrevious: callback triggered when the user clicks Previous.
 * - onNext: callback triggered when the user clicks Next.
 * - onPageChange: callback triggered when the user selects a specific page.
 * - isDisabled: *optional* Disables all pagination buttons. Useful if new page data is loading.
 * 
 * ---------------------------------------------------
 * ------------------- USAGE -------------------------
 * ---------------------------------------------------
 * Place PagePagination inside a <div> to control the pagination's margins.
 *
 * Example:
 *
 * <div className="my-3">
 *      <PagePagination
 *          page={page}
 *          totalPages={totalPages}
 *          hasPreviousPage={hasPreviousPage}
 *          hasNextPage={hasNextPage}
 *          onPrevious={() => setPage(page - 1)}
 *          onNext={() => setPage(page + 1)}
 *          onPageChange={setPage}
 *          isDisabled={isLoading}
 *     />
 * </div>
 *
 */

type PagePaginationProps = {
    page: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    onPrevious: () => void;
    onNext: () => void;
    onPageChange: (page: number) => void;
    isDisabled?: boolean;
}

export default function PagePagination({
    page, 
    totalPages, 
    hasPreviousPage,
    hasNextPage,
    onPrevious,
    onNext,
    onPageChange,
    isDisabled = false,} : PagePaginationProps) {

    const {t} = useTranslation();

    function getPages(){
        if (totalPages <=7) { return Array.from({length: totalPages}, (_, i) => i + 1);}
        if (page <= 3) { return [1,2,3,4, "...", totalPages];}
        if (page >= totalPages -2) { return [1, "...", totalPages -3, totalPages -2, totalPages -1, totalPages];}
        return [1, "...", page - 1, page, page +1, "...", totalPages];
    }

    const pages = getPages();

    return(
        <nav className="flex items-center justify-center gap-2 sm:gap-3">
            <button
                type="button"
                disabled={isDisabled || !hasPreviousPage}
                onClick={onPrevious}
                aria-label={t("aria-label.previous-page")}
                className="cursor-pointer font-semibold text-primary-blue hover:text-nordiska-blue disabled:cursor-not-allowed disabled:opacity-40"
            >
                {t("generic.previous")}
            </button>
            <div className="flex items-center gap-1">
                {pages.map((pageNumber, index) =>
                    pageNumber === "..." 
                    ? (<span key={`ellipsis-${index}`} className="px-2" aria-hidden="true">…</span>) 
                    : (
                        <button
                            key={pageNumber}
                            type="button"
                            disabled={isDisabled}
                            onClick={() => {if (typeof pageNumber === "number") {onPageChange(pageNumber)}}}
                            aria-label={`${t("generic.page")} ${pageNumber}`}
                            aria-current={pageNumber === page ? "page" : undefined}
                            className={`min-w-9 rounded px-2 py-1 font-semibold transition cursor-pointer disabled:cursor-not-allowed disabled:opacity-50 ${pageNumber === page ? "bg-primary-blue text-white" : "text-primary-blue hover:bg-gray-100" }`}
                        >
                                {pageNumber}
                        </button>
                    )
                )}
            </div>
            <button
                type="button"
                disabled={isDisabled || !hasNextPage}
                onClick={onNext}
                aria-label={t("aria-label.next-page")}
                className="cursor-pointer disabled:cursor-not-allowed disabled:opacity-40 font-semibold text-primary-blue hover:text-nordiska-blue"
            >
                {t("generic.next")}
            </button>
        </nav>
    );
}