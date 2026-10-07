import {
    keepPreviousData,
    useMutation,
    useQuery,
    useQueryClient,
} from "@tanstack/react-query";
import {
    createSupportTicket,
    customerSendReply,
    downloadInboxDocument,
    getAllArchivedDocuments,
    getAllSupportThreads,
    getOneSupportThread,
    getUnreadCount,
    markAllAsRead,
    moveThreadToArchive,
    restoreThreadFromArchive,
    staffSendReply,
    type Folders,
} from "../services/inboxService";
import { getFaqCategories } from "../services/faqService";

export const inboxKeys = {
    all: ["inbox"] as const,
    unreadCount: () => [...inboxKeys.all, "unread-count"] as const,
    threads: () => [...inboxKeys.all, "threads"] as const,
    threadList: (folder: Folders, page: number, pageSize: number) =>
        [...inboxKeys.threads(), "list", { folder, page, pageSize }] as const,
    thread: (id: number) => [...inboxKeys.threads(), "detail", id] as const,
    documents: () => [...inboxKeys.all, "documents"] as const,
    documentList: (page: number, pageSize: number, year?: number, documentType?: string) =>
        [...inboxKeys.documents(), "list", { page, pageSize, year, documentType }] as const,
    ticketCategories: (language: string) => [...inboxKeys.all, "ticket-categories", language] as const,
};

// ---------- Queries ----------

export function useUnreadCount() {
    return useQuery({
        queryKey: inboxKeys.unreadCount(),
        queryFn: getUnreadCount,
    });
}

export function useSupportThreads(folder: Folders, page: number, pageSize: number, enabled = true) {
    return useQuery({
        queryKey: inboxKeys.threadList(folder, page, pageSize),
        queryFn: () => getAllSupportThreads(folder, page, pageSize),
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

export function useArchivedDocuments(
    page: number,
    pageSize: number,
    year?: number,
    documentType?: string,
    enabled = true,
) {
    return useQuery({
        queryKey: inboxKeys.documentList(page, pageSize, year, documentType),
        queryFn: () => getAllArchivedDocuments(page, pageSize, year, documentType),
        placeholderData: keepPreviousData,
        enabled,
    });
}

// ---------- Mutations ----------

export function useCreateSupportTicket() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { body: string; category: string; subject: string }) =>
            createSupportTicket(vars.body, vars.category, vars.subject),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
    });
}

export function useCustomerSendReply() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { id: number; body: string }) =>
            customerSendReply(vars.id, vars.body),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
    });
}

export function useStaffSendReply() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { id: number; body: string; replyAllowed: boolean }) =>
            staffSendReply(vars.id, vars.body, vars.replyAllowed),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
    });
}

export function useMarkAllAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => markAllAsRead(id),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
                queryClient.invalidateQueries({ queryKey: inboxKeys.unreadCount() }),
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
                queryClient.invalidateQueries({ queryKey: inboxKeys.unreadCount() }),
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
                queryClient.invalidateQueries({ queryKey: inboxKeys.unreadCount() }),
            ]),
    });
}

export function useDownloadInboxDocument() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => downloadInboxDocument(id),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.documents() }),
    });
}

export function useTicketCategories(language: string) {
    return useQuery({
        queryKey: inboxKeys.ticketCategories(language),
        queryFn: () => getFaqCategories(language),
        staleTime: 5 * 60 * 1000,
    });
}
