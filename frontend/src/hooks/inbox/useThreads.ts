import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
    closeThread,
    createSupportTicket,
    customerSendReply,
    getAllSupportThreads,
    getOneSupportThread,
    markOneThreadAsRead,
    moveThreadToArchive,
    reopenThread,
    restoreThreadFromArchive,
} from "../../services/inboxService";
import type { Folders } from "../../types/inbox";
import { inboxKeys, invalidateSummary } from "./inboxKeys";

// ---------- Queries ----------

export function useSupportThreads(
    folder: Folders | undefined,
    page: number,
    pageSize: number,
    enabled = true,
    searchTerm?: string,
) {
    return useQuery({
        queryKey: inboxKeys.threadList(folder, page, pageSize, searchTerm),
        queryFn: () => getAllSupportThreads(page, pageSize, searchTerm, folder),
        placeholderData: keepPreviousData,
        enabled,
    });
}

export function useSupportThread(id: number | undefined) {
    return useQuery({
        queryKey: inboxKeys.thread(id as number),
        queryFn: () => getOneSupportThread(id as number),
        enabled: id !== undefined,
    });
}

const noop = () => { };

/** Returns function that loads thread into cache ahead of opening it. */
export function usePrefetchSupportThread() {
    const queryClient = useQueryClient();
    return (id: number) => {
        queryClient
            .query({
                queryKey: inboxKeys.thread(id),
                queryFn: () => getOneSupportThread(id),
                // Skip refetch if cached copy is under 30 s old.
                staleTime: 30_000,
            })
            // Prefetch failure is harmless; click fetches again and shows errors.
            .catch(noop);
    };
}

// ---------- Mutations ----------

export function useCreateSupportTicket() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { body: string; category: string; subject: string }) =>
            createSupportTicket(vars.body, vars.category, vars.subject),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
                invalidateSummary(queryClient),
            ]),
    });
}

export function useCustomerSendReply() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { id: number; body: string }) =>
            customerSendReply(vars.id, vars.body),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
                invalidateSummary(queryClient),
            ]),
    });
}

export function useMarkThreadAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => markOneThreadAsRead(id),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
                invalidateSummary(queryClient),
            ]),
    });
}

export function useMoveThreadToArchive() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => moveThreadToArchive(id),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
                invalidateSummary(queryClient),
            ]),
    });
}

export function useRestoreThreadFromArchive() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => restoreThreadFromArchive(id),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
                invalidateSummary(queryClient),
            ]),
    });
}

export function useCloseThread() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => closeThread(id),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
    });
}

export function useReopenThread() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => reopenThread(id),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
    });
}
