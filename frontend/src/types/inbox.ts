export type Categories = "Sparkonto" | "Sparmål" | "Skatteunderlag" | "Allmänt";

export type FeedItemTypes = "thread" | "notification" | "document" | "term";

export type Folders = "inbox" | "sent" | "archive";

export interface FeedCounts {
    unreadCount: number;
    totalUnread: number;
    unreadThreads: number;
    unreadNotifications: number;
    unopenedDocuments: number;
    pendingTerms: number;
    unreadDocuments: number;
    unreadTerms: number;
    actionRequired: number;
}

export interface FeedItem {
    id: number;
    type: FeedItemTypes | null;
    sourceId: number;
    title: string | null;
    preview: string | null;
    category: Categories | null;
    priority: string | null;
    isRead: boolean;
    actionRequired: boolean;
    occuredAt: string;
    targetUrl: string | null;
    createdAt: string;
}


//OVERVIEW
export interface UnifiedOverview {
    counts: FeedCounts;
    feed: FeedItem[];
    unreadFeed: FeedItem[];
    pendingTerms: Term[];
}

//FEED
export interface UnifiedFeed {
    items: FeedItem[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export interface MarkAllReadResponse {
    success: boolean;
    threadsMarkedAsRead: number;
    notificationsMarkedAsRead: number;
    totalMarkedAsRead: number;
    message: string;
    documentsMarkedAsRead: number;
    termsMarkedAsRead: number;
}

//Threads


export interface SupportThreadsResponse {
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
    isInformationOnly: boolean;
    canReply: boolean;
    category: Categories | null;
    title: string | null;
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

//DOCUMENTS
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
    createdAt: string;
    isRead: boolean;
    category: Categories | null;
    downloadUrl: string | null;
}

export type GeneralDocument = {
    id: number;
    documentType: string | null;
    title: string | null;
    fileName: string | null;
    mimeType: string | null;
    fileSizeBytes: number;
    sha256: string | null;
    publishedAt: string;
    downloadUrl: string | null;
    createdAt: string;
    isRead: boolean;
    category: Categories | null;
};

// NOTIFICATIONS
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

//TERMS
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
    isRead: boolean;
    category: Categories | null;
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

//ADMIN
export interface AdminThreadSummary {
    id: number;
    customerId: number;
    customerName: string | null;
    subject: string | null;
    status: string | null;
    category: Categories | null;
    isInformationOnly: boolean;
    canReply: boolean;
    messageCount: number;
    createdAt: string;
    lastMessageAt: string;
}

export interface AdminThreadsResponse {
    items: AdminThreadSummary[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}
