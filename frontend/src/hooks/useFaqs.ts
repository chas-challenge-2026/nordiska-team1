import { useQuery, useMutation, keepPreviousData } from "@tanstack/react-query";
import { getFaqs, viewFaq, getFaqAnalyticsSummary, getNoResultFaqs, type AnalyticsParams } from "../services/faqService";
import { isAxiosError } from "axios";

export const useFaqs = (
    language: string,
    page: number,
    pageSize?: number,
    search?: string,
    category?: string,
    keyword?: string,
    sortBy?: string,
    sortOrder?: "asc" | "desc",
) => {

    return useQuery({

        // Identifierare för datan
        // håller koll på vilka querys som kommit in från användaren
        queryKey: ["faqs", language, page, pageSize, search, category, keyword, sortBy, sortOrder],

        // När React Query behöver hämta FAQ-data, kör den här funktionen.
        // Kör getFaqs(page, 10, search, category)
        // får tillbaka: Faq (type)
        queryFn: () =>
            getFaqs({
                language,
                page,
                pageSize: pageSize ?? 5,
                search,
                category,
                keyword,
                sortBy,
                sortOrder
            }),

        enabled: !!language,
    });
};


export function useViewFaq() {
    return useMutation({
        mutationFn: viewFaq,
    })
}

function retryUnlessAuthError(failureCount: number, error: unknown) {
    if (isAxiosError(error) && (error.response?.status === 401 || error.response?.status === 403)) {
        return false;
    }
    return failureCount < 3;
}

export const useFaqAnalyticsSummary = (params: AnalyticsParams = {}) => {
    return useQuery({
        queryKey: ["faqAnalytics", "summary", params.lang, params.days, params.limit],
        queryFn: () => getFaqAnalyticsSummary(params),
        placeholderData: keepPreviousData,
        retry: retryUnlessAuthError,
    });
};

export const useFaqContentGaps = (params: AnalyticsParams = {}) => {
    return useQuery({
        queryKey: ["faqAnalytics", "contentGaps", params.lang, params.days, params.limit],
        queryFn: () => getNoResultFaqs(params),
        placeholderData: keepPreviousData,
        retry: retryUnlessAuthError,
    });
};
