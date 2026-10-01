import { useQuery } from "@tanstack/react-query";
import { getFaqCategories } from "../services/faqService";

export const useFaqCategories = (language: string) => {
    return useQuery({
        queryKey: ["faqCategories", language],
        queryFn: () => getFaqCategories(language),
        enabled: !!language,
    });
};