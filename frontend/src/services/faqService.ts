import axiosInstance from "./axiosInstance";


export type Faq = {
    id: number;
    question: string | null;
    answer: string | null;
    category: string | null;
    helpfulCount: number;
    keywords: string[] | null;
};

type  FaqSearchParams = {
    searchTerm?: string;
    category?: string;
    keyword?: string;
}

export async function searchFaqs(params?: FaqSearchParams) {
    const res = await axiosInstance.get<Faq[]>("faqs/search", {
        params,
    })

    return res.data;
}