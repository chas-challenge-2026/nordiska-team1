import axiosInstance from "./axiosInstance";
import type {
    AcceptedTerm,
    DocumentsResponse,
    Folders,
    Message,
    NotificationsResponse,
    PublishedTerm,
    SupportThreadsResponse,
    SupportTicketThread,
    Term,
    UnifiedOverview,
    UnifiedFeed,
    FeedCounts,
    FeedItemTypes,
    MarkAllReadResponse,
    AdminThreadsResponse,
    GeneralDocument,
} from "../types/inbox";

export async function getUnreadCount(): Promise<FeedCounts> {
    const res = await axiosInstance.get("/inbox/unread-count");
    return res.data;
}

export async function getAllSupportThreads(page: number, pageSize: number, searchTerm?: string, folder?: Folders): Promise<SupportThreadsResponse> {
    const res = await axiosInstance.get("/inbox/threads", { params: { folder, page, pageSize, searchTerm } });
    return res.data;
}

export async function createSupportTicket(body: string, category: string, subject: string): Promise<SupportTicketThread> {
    const res = await axiosInstance.post("/inbox/threads", { body, category, subject });
    return res.data;
}

export async function getOneSupportThread(id: number): Promise<SupportTicketThread> {
    const res = await axiosInstance.get(`/inbox/threads/${id}`);
    return res.data;
}

export async function customerSendReply(id: number, body: string): Promise<Message> {
    const res = await axiosInstance.post(`/inbox/threads/${id}/messages`, { body });
    return res.data;
}

export async function markOneThreadAsRead(id: number): Promise<void> {
    await axiosInstance.patch(`/inbox/threads/${id}/read`);
}

export async function moveThreadToArchive(id: number): Promise<void> {
    await axiosInstance.patch(`/inbox/threads/${id}/archive`);
}

export async function restoreThreadFromArchive(id: number): Promise<void> {
    await axiosInstance.patch(`/inbox/threads/${id}/restore`);
}

export async function closeThread(id: number): Promise<void> {
    await axiosInstance.patch(`/inbox/threads/${id}/close`);
}

export async function reopenThread(id: number): Promise<void> {
    await axiosInstance.patch(`/inbox/threads/${id}/reopen`);
}

//Admin

export async function staffGetAllThreads(Page: number, PageSize: number, CustomerId?: number, Status?: "Open" | "Closed", SearchTerm?: string): Promise<AdminThreadsResponse> {
    const res = await axiosInstance.get("/inbox/admin/threads", { params: { Page, PageSize, CustomerId, Status, SearchTerm } });
    return res.data;
}

export async function staffGetThread(id: number): Promise<SupportTicketThread> {
    const res = await axiosInstance.get(`/inbox/admin/threads/${id}`);
    return res.data;
}

export async function staffSendReply(id: number, body: string, replyAllowed: boolean): Promise<Message> {
    const res = await axiosInstance.post(`/inbox/threads/${id}/staff-reply`, { body, replyAllowed });
    return res.data;
}

export async function createBroadcastMessage(
    body: string | null,
    broadcastToAll: boolean,
    category: string | null,
    customerId: number | null,
    isInformationOnly: boolean,
    replyAllowed: boolean,
    subject: string | null
): Promise<void> {
    await axiosInstance.post("/inbox/admin/threads", { body, broadcastToAll, category, customerId, isInformationOnly, replyAllowed, subject });
}

//end admin

export async function getAllNotifications(unreadOnly: boolean, page: number, pageSize: number): Promise<NotificationsResponse> {
    const res = await axiosInstance.get("/inbox/notifications", { params: { unreadOnly, page, pageSize } });
    return res.data;
}

export async function markNotificationAsRead(notificationId: number): Promise<void> {
    await axiosInstance.patch(`/inbox/notifications/${notificationId}/read`);
}

export async function markAllNotificationsAsRead(): Promise<void> {
    await axiosInstance.patch(`/inbox/notifications/read-all`);
}

export async function getAllArchivedDocuments(Page: number, PageSize: number, Year?: number, DocumentType?: string): Promise<DocumentsResponse> {
    const res = await axiosInstance.get("/inbox/documents", { params: { Year, DocumentType, Page, PageSize } });
    return res.data;
}

export async function downloadInboxDocument(id: number): Promise<void> {
    const res = await axiosInstance.get<Blob>(
        `/inbox/documents/${id}/download`,
        {
            responseType: "blob",
            headers: { Accept: "application/pdf" },
            timeout: 60000,
        }
    );

    const url = URL.createObjectURL(res.data);
    const link = document.createElement("a");
    link.href = url;
    link.download = `document-${id}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}

export async function getAllGeneralDocuments(): Promise<GeneralDocument[]> {
    const res = await axiosInstance.get("/inbox/documents/general");
    return res.data;
}

export async function getAllPendingTerms(): Promise<Term[]> {
    const res = await axiosInstance.get("/inbox/terms/pending");
    return res.data;
}

export async function acceptPendingTerm(termId: number): Promise<AcceptedTerm> {
    const res = await axiosInstance.post(`/inbox/terms/${termId}/accept`);
    return res.data;
}

export async function publishNewTerms(code: string, documentId: number, effectiveFrom: string, title: string, version: number): Promise<PublishedTerm> {
    const res = await axiosInstance.post("/inbox/terms/publish", { code, documentId, effectiveFrom, title, version });
    return res.data;
}

export async function getUnifiedOverview(): Promise<UnifiedOverview> {
    const res = await axiosInstance.get("/inbox/overview");
    return res.data;
}

export async function getUnifiedInbox(page: number, pageSize: number, type?: FeedItemTypes, unreadOnly?: boolean): Promise<UnifiedFeed> {
    const res = await axiosInstance.get("/inbox/feed", { params: { page, pageSize, type, unreadOnly } });
    return res.data;
}

export async function markOneUnifiedAsRead(id: string): Promise<void> {
    await axiosInstance.patch(`/inbox/feed/${id}/read`)
}

export async function markAllItemsAsRead(): Promise<MarkAllReadResponse> {
    const res = await axiosInstance.post("/inbox/read-all");
    return res.data;
}
