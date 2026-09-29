import axiosInstance from "./axiosInstance";
import type { FaqResponse } from "../types/types";

export type GetFaqParams = {
    language: string;
    page: number;
    pageSize: number;
    search?: string;
    category?: string;
    keyword?: string;
};

type HelpAction = "increase" | "decrease";

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

export async function getFaqCategories(
    language: string
): Promise<string[]> {
    const res = await axiosInstance.get<string[]>(
        `faqs/${language}/categories`
    );

    return res.data;
}


export async function logHelpCount(id:number, action: HelpAction): Promise<number> {
    const res = await axiosInstance.patch(`faqs/${id}/${action}`);

    console.log(res.data.helpfulCount)
    return res.status
} 