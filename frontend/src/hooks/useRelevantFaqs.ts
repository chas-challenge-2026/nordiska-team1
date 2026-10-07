import { useQuery } from "@tanstack/react-query";
import { getRelevantFaqs } from "../services/faqService";

export function useRelevantFaqs(
    language: string,
    searchTerms: string,
    numOfHits: number,
) {
    return useQuery({
        queryKey: ["relevantFaqs", language, searchTerms],
        queryFn: () => getRelevantFaqs(language, searchTerms, numOfHits),
        enabled: !!language && !!searchTerms,
    });
}