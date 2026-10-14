import type { QueryClient } from "@tanstack/react-query";
import type { FeedItemTypes, Folders } from "../../types/inbox";

export type StaffThreadStatus = "Open" | "Closed";

export const inboxKeys = {
    all: ["inbox"] as const,
    unreadCount: () => [...inboxKeys.all, "unread-count"] as const,
    overview: () => [...inboxKeys.all, "overview"] as const,
    feed: () => [...inboxKeys.all, "feed"] as const,
    feedList: (page: number, pageSize: number, type?: FeedItemTypes, unreadOnly?: boolean) =>
        [...inboxKeys.feed(), "list", { page, pageSize, type, unreadOnly }] as const,
    threads: () => [...inboxKeys.all, "threads"] as const,
    threadList: (folder: Folders | undefined, page: number, pageSize: number, searchTerm?: string) =>
        [...inboxKeys.threads(), "list", { folder, page, pageSize, searchTerm }] as const,
    adminThreadList: (page: number, pageSize: number, customerId?: number, status?: StaffThreadStatus, searchTerm?: string) =>
        [...inboxKeys.threads(), "admin-list", { page, pageSize, customerId, status, searchTerm }] as const,
    thread: (id: number) => [...inboxKeys.threads(), "detail", id] as const,
    adminThread: (id: number) => [...inboxKeys.threads(), "admin-detail", id] as const,
    documents: () => [...inboxKeys.all, "documents"] as const,
    documentList: (page: number, pageSize: number, year?: number, documentType?: string) =>
        [...inboxKeys.documents(), "list", { page, pageSize, year, documentType }] as const,
    generalDocuments: () => [...inboxKeys.documents(), "general"] as const,
    notifications: () => [...inboxKeys.all, "notifications"] as const,
    notificationList: (unreadOnly: boolean, page: number, pageSize: number) =>
        [...inboxKeys.notifications(), "list", { unreadOnly, page, pageSize }] as const,
    terms: () => [...inboxKeys.all, "terms"] as const,
    pendingTerms: () => [...inboxKeys.terms(), "pending"] as const,
    ticketCategories: (language: string) => [...inboxKeys.all, "ticket-categories", language] as const,
};

/** Counts, overview and feed span every item type, so any read-state change refreshes them. */
export function invalidateSummary(queryClient: QueryClient) {
    return Promise.all([
        queryClient.invalidateQueries({ queryKey: inboxKeys.unreadCount() }),
        queryClient.invalidateQueries({ queryKey: inboxKeys.overview() }),
        queryClient.invalidateQueries({ queryKey: inboxKeys.feed() }),
    ]);
}
