import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import {
    createTransaction,
    getTransactions,
    transferFunds,
    createPlannedTransaction,
    cancelPlannedTransaction,
    type TransactionParams,
} from "../services/transactionsService";
import { accountKey } from "./useAccounts";

export const transactionKey = {
    all: ["transactions"] as const,
    list: (params: TransactionParams) => [...transactionKey.all, "list", params] as const,
};


export function useTransactions(params: TransactionParams, enabled = true) {
    return useQuery({
        queryKey: transactionKey.list(params),
        queryFn: () => getTransactions(params),
        placeholderData: keepPreviousData,
        enabled,
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

export function useTransferFunds() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: transferFunds,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: transactionKey.all });
            queryClient.invalidateQueries({ queryKey: accountKey.all });
        },
    });
}

export function useCreatePlannedTransaction() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: createPlannedTransaction,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: transactionKey.all });
        },
    });
}

export function useCancelPlannedTransaction() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: cancelPlannedTransaction,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: transactionKey.all });
        },
    });
}
