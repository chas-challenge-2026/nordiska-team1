import {
    keepPreviousData,
    useMutation,
    useQuery,
    useQueryClient,
} from "@tanstack/react-query";
import {
    acceptPendingTerm,
    closeThread,
    createBroadcastMessage,
    createSupportTicket,
    customerSendReply,
    downloadInboxDocument,
    getAllArchivedDocuments,
    getAllNotifications,
    getAllPendingTerms,
    getAllSupportThreads,
    getOneSupportThread,
    getUnreadCount,
    markAllAsRead,
    markAllNotificationsAsRead,
    markNotificationAsRead,
    moveThreadToArchive,
    publishNewTerms,
    reopenThread,
    restoreThreadFromArchive,
    staffGetAllThreads,
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
    adminThreadList: (page: number, pageSize: number, customerId?: number, status?: string, searchTerm?: string) =>
        [...inboxKeys.threads(), "admin-list", { page, pageSize, customerId, status, searchTerm }] as const,
    thread: (id: number) => [...inboxKeys.threads(), "detail", id] as const,
    documents: () => [...inboxKeys.all, "documents"] as const,
    documentList: (page: number, pageSize: number, year?: number, documentType?: string) =>
        [...inboxKeys.documents(), "list", { page, pageSize, year, documentType }] as const,
    notifications: () => [...inboxKeys.all, "notifications"] as const,
    notificationList: (unreadOnly: boolean, page: number, pageSize: number) =>
        [...inboxKeys.notifications(), "list", { unreadOnly, page, pageSize }] as const,
    terms: () => [...inboxKeys.all, "terms"] as const,
    pendingTerms: () => [...inboxKeys.terms(), "pending"] as const,
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

export function useStaffThreads(
    page: number,
    pageSize: number,
    customerId?: number,
    status?: string,
    searchTerm?: string,
    enabled = true,
) {
    return useQuery({
        queryKey: inboxKeys.adminThreadList(page, pageSize, customerId, status, searchTerm),
        queryFn: () => staffGetAllThreads(page, pageSize, customerId, status, searchTerm),
        placeholderData: keepPreviousData,
        enabled,
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

export function useNotifications(unreadOnly: boolean, page: number, pageSize: number, enabled = true) {
    return useQuery({
        queryKey: inboxKeys.notificationList(unreadOnly, page, pageSize),
        queryFn: () => getAllNotifications(unreadOnly, page, pageSize),
        placeholderData: keepPreviousData,
        enabled,
    });
}

export function usePendingTerms(enabled = true) {
    return useQuery({
        queryKey: inboxKeys.pendingTerms(),
        queryFn: getAllPendingTerms,
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

export function useCreateBroadcastMessage() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: {
            body: string | null;
            broadcastToAll: boolean;
            category: string | null;
            customerId: number | null;
            isInformationOnly: boolean;
            replyAllowed: boolean;
            subject: string | null;
        }) =>
            createBroadcastMessage(
                vars.body,
                vars.broadcastToAll,
                vars.category,
                vars.customerId,
                vars.isInformationOnly,
                vars.replyAllowed,
                vars.subject,
            ),
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

export function useMarkNotificationAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (notificationId: number) => markNotificationAsRead(notificationId),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.notifications() }),
                queryClient.invalidateQueries({ queryKey: inboxKeys.unreadCount() }),
            ]),
    });
}

export function useMarkAllNotificationsAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => markAllNotificationsAsRead(),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.notifications() }),
                queryClient.invalidateQueries({ queryKey: inboxKeys.unreadCount() }),
            ]),
    });
}

export function useAcceptPendingTerm() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (termId: number) => acceptPendingTerm(termId),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.terms() }),
    });
}

export function usePublishNewTerms() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { code: string; documentId: number; effectiveFrom: string; title: string; version: number }) =>
            publishNewTerms(vars.code, vars.documentId, vars.effectiveFrom, vars.title, vars.version),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.terms() }),
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
