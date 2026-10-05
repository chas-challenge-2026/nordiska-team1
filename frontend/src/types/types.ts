export interface User {
    id: number;
    email: string;
    role?: string;
    name?: string;
    phone?: string;
}

export type Faq = {
    id: number;
    question: string | null;
    answer: string | null;
    category: string | null;
    helpfulCount: number;
    keywords: string[] | null;
    relationId: string;
    lang: "sv" | "en";
};

export type FaqGroup = {
    relationId: string;
    faqs: Faq[];
};

export type FaqResponse = {
    items: Faq[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
};