import { useQuery } from "@tanstack/react-query";
import { getFaqCategories } from "../../services/faqService";
import { inboxKeys } from "./inboxKeys";

export function useTicketCategories(language: string) {
    return useQuery({
        queryKey: inboxKeys.ticketCategories(language),
        queryFn: () => getFaqCategories(language),
        staleTime: 5 * 60 * 1000,
    });
}
