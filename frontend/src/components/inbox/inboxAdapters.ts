// All guesses about notification and term response shapes live here.
// Fix field names in this file when real types are known.

type Raw = Record<string, unknown>;

function str(raw: Raw, ...keys: string[]): string | null {
    for (const key of keys) {
        const value = raw[key];
        if (typeof value === "string" && value !== "") return value;
    }
    return null;
}

function num(raw: Raw, ...keys: string[]): number | null {
    for (const key of keys) {
        const value = raw[key];
        if (typeof value === "number" && Number.isFinite(value)) return value;
    }
    return null;
}

/** Accepts plain array or paged `{ items: [] }` response. */
export function itemsOf(data: unknown): Raw[] {
    if (Array.isArray(data)) return data as Raw[];
    if (data && typeof data === "object" && Array.isArray((data as Raw).items)) {
        return (data as Raw).items as Raw[];
    }
    return [];
}

export type Paging = {
    page: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
};

/** Null when response is not paged. */
export function pagingOf(data: unknown): Paging | null {
    if (!data || typeof data !== "object" || Array.isArray(data)) return null;
    const raw = data as Raw;
    const page = num(raw, "page");
    const totalPages = num(raw, "totalPages");
    if (page === null || totalPages === null) return null;
    return {
        page,
        totalPages,
        hasPreviousPage: raw.hasPreviousPage === true,
        hasNextPage: raw.hasNextPage === true,
    };
}

export type NotificationView = {
    id: number;
    title: string;
    body: string | null;
    date: string | null;
    isRead: boolean;
};

export function toNotificationViews(data: unknown): NotificationView[] {
    return itemsOf(data).flatMap((raw) => {
        const id = num(raw, "id", "notificationId");
        if (id === null) return [];
        const body = str(raw, "body", "message", "text", "content");
        const isRead = typeof raw.isRead === "boolean" ? raw.isRead : raw.readAt != null;
        return [{
            id,
            title: str(raw, "title", "subject") ?? body ?? "(untitled)",
            body: str(raw, "title", "subject") ? body : null,
            date: str(raw, "createdAt", "sentAt", "publishedAt"),
            isRead,
        }];
    });
}

export type TermView = {
    id: number;
    title: string;
    version: number | null;
    effectiveFrom: string | null;
    documentId: number | null;
};

export function toTermViews(data: unknown): TermView[] {
    return itemsOf(data).flatMap((raw) => {
        const id = num(raw, "id", "termId");
        if (id === null) return [];
        return [{
            id,
            title: str(raw, "title", "code") ?? "(untitled)",
            version: num(raw, "version"),
            effectiveFrom: str(raw, "effectiveFrom"),
            documentId: num(raw, "documentId"),
        }];
    });
}
