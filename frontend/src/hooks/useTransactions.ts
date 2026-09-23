import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createTransaction,
    getTransactions,
    transferFunds,
    createPlannedTransaction,
    cancelPlannedTransaction,
} from "../services/transactionsService";
import { accountKey } from "./useAccounts";

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
