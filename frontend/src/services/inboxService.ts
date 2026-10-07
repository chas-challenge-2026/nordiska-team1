import axiosInstance from "./axiosInstance";

interface SupportThreadsResponse {
    items: Thread[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export type Thread = {
    id: number;
    subject: string | null;
    status: string | null;
    createdAt: string;
    lastMessageAt: string;
    isRead: boolean;
    folder: Folders | null;
    messageCount: number;
}

export type Message = {
    id: number;
    threadId: number;
    senderType: string | null;
    senderName: string | null;
    senderCustomerId: number | null;
    body: string | null;
    replyAllowed: boolean;
    sentAt: string
}

export interface SupportTicketThread extends Thread {
    canReply: boolean;
    messages: Message[];
}

export interface DocumentsResponse {
    items: InboxDocument[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export type InboxDocument = {
    id: number;
    documentId: number;
    documentType: string | null;
    title: string | null;
    fileName: string | null;
    mimeType: string | null;
    fileSizeBytes: number;
    sha256: string | null;
    status: string | null;
    publishedAt: string;
    firstOpenedAt: string | null;
    hasBeenOpened: boolean;
}

export type Folders = "inbox" | "sent" | "archive";

export type Notification = {
    id: number;
    customerId: number;
    type: string | null;
    priority: number | null;
    title: string | null;
    body: string | null;
    targetType: string | null;
    targetId: number | null;
    isRead: boolean;
    createdAt: string;
    readAt: string | null;
}

export interface NotificationsResponse {
    items: Notification[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export interface Term {
    acceptanceId: number;
    termId: number;
    code: string | null;
    version: number;
    title: string | null;
    documentId: number;
    effectiveFrom: string;
    publishedAt: string;
    status: string | null;
    createdAt: string | null;
    downloadUrl: string | null;
}

export interface AcceptedTerm {
    acceptanceId: number;
    termId: number;
    customerId: number;
    status: string | null;
    acceptedAt: string | null;
    success: boolean;
    message: string | null;
}

export interface PublishedTerm {
    id: number;
    code: string | null;
    version: number;
    title: string | null;
    documentId: number;
    status: string | null;
    effectiveFrom: string;
    publishedAt: string;
    downloadUrl: string | null;
}

export async function getUnreadCount(): Promise<number> {
    const res = await axiosInstance.get<{ unreadCount: number }>("/inbox/unread-count");
    return res.data.unreadCount;
}

export async function getAllSupportThreads(folder: Folders, page: number, pageSize: number): Promise<SupportThreadsResponse> {
    const res = await axiosInstance.get("/inbox/threads", { params: { folder, page, pageSize } });
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

export async function markAllAsRead(id: number): Promise<void> {
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

export async function staffGetAllThreads(Page: number, PageSize: number, CustomerId?: number, Status?: string, SearchTerm?: string): Promise<SupportThreadsResponse> {
    const res = await axiosInstance.get("/inbox/admin/threads", { params: { Page, PageSize, CustomerId, Status, SearchTerm } });
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

export async function getAllNotifications(unreadOnly: boolean, page: number, pageSize: number): Promise<NotificationsResponse> {
    const res = await axiosInstance.get("/inbox/notifications", { params: { unreadOnly, page, pageSize } })
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
