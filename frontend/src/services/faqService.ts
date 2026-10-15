import axiosInstance from "./axiosInstance";
import type { FaqResponse} from "../types/types";

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

// ------ GET MATCHING FAQ PAIR ------
// -----------------------------------
export async function getFaqsByRelationId(relationId: string){
    const res = await axiosInstance.get(`/faqs/relation/${relationId}`);
    return res.data;
}

// ------ GET RELEVANT FAQS ------
// -------------------------------
export async function getRelevantFaqs(language: string, searchTerms: string, numOfHits: number,) {
    const terms = searchTerms.split(/\s+/).filter(Boolean);
    const results = await Promise.all(
        terms.map((term) =>
            axiosInstance.get<FaqResponse>(
                `faqs/${language}`,
                {
                    params: {
                        page: 1,
                        pageSize: {numOfHits},
                        search: term,
                    },
                }
            )
        )
    );
    const faqs = results.flatMap((res) => res.data.items);
    // Ta bort dubbletter
    return Array.from( new Map(faqs.map((faq) => [faq.id, faq])).values()).slice(0, numOfHits);
}

// ------ GET CATEGORIES ------
// ----------------------------
export async function getFaqCategories(
    language: string
): Promise<string[]> {
    const res = await axiosInstance.get<string[]>(`faqs/${language}/categories`);
    return res.data;
}

// ------ INCREASE/DECREASE HELPCOUNT ------
// -----------------------------------------
type HelpAction = "increase" | "decrease";

export async function logHelpCount(id:number, action: HelpAction): Promise<number> {
    const res = await axiosInstance.patch(`faqs/${id}/${action}`);
    return res.status
}

// ------ CREATE FAQ ------
// ------------------------
export type CreateFaqRequest = {
    question: string;
    answer: string;
    category: string;
    keywords: string;
    lang: "sv" | "en";
    relationId: string;
};

export async function createFaq(faq: CreateFaqRequest) {
    const res = await axiosInstance.post("/faq", faq);
    return res.data;
}

// ------ EDIT FAQ ------
// ----------------------
type EditFaqRequest = {
    question: string;
    answer: string;
    category: string;
    keywords: string;
};

export async function editFaq(
    id: number,
    lang: string,
    faq: EditFaqRequest
) {
    const res = await axiosInstance.patch(`/faq/${lang}/${id}`, faq);
    return res.data;
}

// ------ DELETE FAQ ------
// ------------------------
export async function deleteFaq(id: number) {
    const res = await axiosInstance.delete(`/faq/${id}`);
    return res.data;
}