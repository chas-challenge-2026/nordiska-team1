import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { createTransaction, getTransactions } from "../services/transactionsService";

export const transactionKey = {
    all: ["transactions"] as const,
};

export function useTransactions() {
    return useQuery({
        queryKey: transactionKey.all,
        queryFn: getTransactions,
    });
}

export function useCreateTransaction() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: createTransaction,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: transactionKey.all });
        },
    })
}
