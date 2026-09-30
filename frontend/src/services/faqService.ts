import axiosInstance from "./axiosInstance";
import type { FaqResponse } from "../types/types";

/*
Services for
    - getFaqs
    - getFaqCategories
    - logHelpCount
*/

// -----------------------
// ------ GET FAQ:s ------
// -----------------------
export type GetFaqParams = {
    language: string;
    page: number;
    pageSize: number;
    search?: string;
    category?: string;
    keyword?: string;
};

export async function getFaqs(
    params: GetFaqParams
): Promise<FaqResponse> {
    const res = await axiosInstance.get<FaqResponse>(
        `faqs/${params.language}`,
        {
            params: {
                page: params.page,
                pageSize: params.pageSize,
                ...(params.search && { search: params.search }),
                ...(params.category && { category: params.category }),
                ...(params.keyword && { keyword: params.keyword }),
            },
        }
    );

    return res.data;
}

// ----------------------------
// ------ GET CATEGORIES ------
// ----------------------------
export async function getFaqCategories(
    language: string
): Promise<string[]> {
    const res = await axiosInstance.get<string[]>(`faqs/${language}/categories`);
    return res.data;
}

// -----------------------------------------
// ------ INCREASE/DECREASE HELPCOUNT ------
// -----------------------------------------
type HelpAction = "increase" | "decrease";

export async function logHelpCount(id:number, action: HelpAction): Promise<number> {
    const res = await axiosInstance.patch(`faqs/${id}/${action}`);

    return res.status
}

// ------------------------
// ------ CREATE FAQ ------
// ------------------------
export type CreateFaqRequest = {
    question: string;
    answer: string;
    category: string;
    keywords: string;
    lang: "sv" | "en";
};

export async function createFaq(faq: CreateFaqRequest) {
    const res = await axiosInstance.post("/faqs", faq);
    return res.data;
}


