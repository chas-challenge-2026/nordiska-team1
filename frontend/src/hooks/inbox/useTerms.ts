import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { acceptPendingTerm, getAllPendingTerms } from "../../services/inboxService";
import { inboxKeys, invalidateSummary } from "./inboxKeys";

export function usePendingTerms(enabled = true) {
    return useQuery({
        queryKey: inboxKeys.pendingTerms(),
        queryFn: getAllPendingTerms,
        enabled,
    });
}

export function useAcceptPendingTerm() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (termId: number) => acceptPendingTerm(termId),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.terms() }),
                invalidateSummary(queryClient),
            ]),
    });
}
