import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import {
    createTransaction,
    getTransactions,
    transferFunds,
    createPlannedTransaction,
    cancelPlannedTransaction,
    type Transaction,
    type TransactionParams,
} from "../services/transactionsService";
import { useMemo } from "react";
import { mergeTransferPairs, groupRowsByDate } from "../utils/transactions";
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

const FETCH_ALL_PAGE_SIZE = 100; // backendens maxgräns per anrop
const FETCH_ALL_MAX_PAGES = 50;

type TransactionFilterParams = Omit<TransactionParams, "Page" | "PageSize">;

async function getAllTransactions(filters: TransactionFilterParams): Promise<Transaction[]> {
    const items: Transaction[] = [];
    for (let page = 1; page <= FETCH_ALL_MAX_PAGES; page++) {
        const res = await getTransactions({ ...filters, Page: page, PageSize: FETCH_ALL_PAGE_SIZE });
        items.push(...res.items);
        if (!res.hasNextPage) break;
    }
    return items;
}

/**
 * Hämtar alla transaktioner för filtren, slår ihop interna överföringar till en rad
 * och delar upp raderna i sidor lokalt. Backend paginerar varje ben för sig, så
 * pagineringen måste ske efter sammanslagningen för att ge rätt antal rader per sida.
 */
export function useTransactionRows(filters: TransactionFilterParams, page: number, pageSize: number, enabled = true) {
    const query = useQuery({
        queryKey: [...transactionKey.all, "all", filters] as const,
        queryFn: () => getAllTransactions(filters),
        // Behåll föregående data bara när queryn är aktiv, annars visas gammal data gråad
        placeholderData: enabled ? keepPreviousData : undefined,
        enabled,
    });

    const items = enabled ? query.data : undefined;
    const rows = useMemo(() => mergeTransferPairs(items ?? []), [items]);
    const groups = useMemo(() => groupRowsByDate(rows.slice((page - 1) * pageSize, page * pageSize)), [rows, page, pageSize]);

    const totalCount = rows.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

    return {
        groups,
        shownCount: groups.reduce((sum, group) => sum + group.rows.length, 0),
        totalCount,
        totalPages,
        hasNextPage: page < totalPages,
        hasPreviousPage: page > 1,
        isLoading: enabled && query.isLoading,
        isError: enabled && query.isError,
        isPlaceholderData: enabled && query.isPlaceholderData,
    };
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
