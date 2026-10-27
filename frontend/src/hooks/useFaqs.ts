import { useQuery } from "@tanstack/react-query";
import { getFaqs } from "../services/faqService";

export const useFaqs = (
    language: string,
    page: number,
    pageSize?: number,
    search?: string,
    category?: string,
    keyword?: string
) => {

    return useQuery({

        // Identifierare för datan
        // håller koll på vilka querys som kommit in från användaren
        queryKey: ["faqs", language, page, pageSize, search, category, keyword],

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
            }),

            enabled: !!language,
    });
};

