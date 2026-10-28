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
import { mergeTransferPairs, groupRowsByDate, type TransactionRow } from "../utils/transactions";
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

/** Alla genomförda transaktioner för ett konto från och med fromDate (ISO-sträng) */
export function useAccountTransactionsSince(accountId: number, fromDate: string) {
    const filters: TransactionFilterParams = { AccountIds: [accountId], IsPlanned: false, FromDate: fromDate };
    return useQuery({
        queryKey: [...transactionKey.all, "all", filters] as const,
        queryFn: () => getAllTransactions(filters),
    });
}

interface TransactionRowsOptions {
    closedAccountIds: ReadonlySet<number>;
    /** Insättningar/uttag filtreras på beloppets tecken, så att även överföringsben och ränta/skatt kommer med */
    direction?: "deposit" | "withdrawal";
    enabled?: boolean;
}

/**
 * Hämtar alla transaktioner för filtren, slår ihop interna överföringar till en rad
 * och delar upp raderna i sidor lokalt. Backend paginerar varje ben för sig, så
 * pagineringen måste ske efter sammanslagningen för att ge rätt antal rader per sida.
 * Filtreras det på insättningar eller uttag visas varje ben för sig utan sammanslagning.
 */
export function useTransactionRows(filters: TransactionFilterParams, page: number, pageSize: number, { closedAccountIds, direction, enabled = true }: TransactionRowsOptions) {
    const query = useQuery({
        queryKey: [...transactionKey.all, "all", filters] as const,
        queryFn: () => getAllTransactions(filters),
        // Behåll föregående data bara när queryn är aktiv, annars visas gammal data gråad
        placeholderData: enabled ? keepPreviousData : undefined,
        enabled,
    });

    const items = enabled ? query.data : undefined;
    const rows = useMemo<TransactionRow[]>(() => {
        if (!direction) return mergeTransferPairs(items ?? [], closedAccountIds);
        return (items ?? [])
            .filter(tx => direction === "deposit" ? tx.amount > 0 : tx.amount < 0)
            .map(tx => ({ kind: "single", tx }));
    }, [items, closedAccountIds, direction]);
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
            // Saldo och ränta räknas om av backend, så kontona måste hämtas på nytt
            queryClient.invalidateQueries({ queryKey: accountKey.all });
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
