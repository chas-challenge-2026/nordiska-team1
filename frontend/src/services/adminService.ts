import axiosInstance from "./axiosInstance";

export async function getOperationalMessage() {
    const res = await axiosInstance.get("/operational-messages");
    return res.data;
}

export type CreateOperationalMessageRequest = {
    titleSv: string | null;
    titleEn: string | null;
    messageSv: string | null;
    messageEn: string | null;
    severity: string | null;
    isActive: boolean;
    priority: number;
    startDate: string | null;
    endDate: string | null;
};

export async function createOperationalMessage(payload: CreateOperationalMessageRequest) {
    const res = await axiosInstance.post("/operational-messages",payload);
    return res.data;
}

export type PatchOperationalMessageRequest = {
    titleSv?: string | null;
    titleEn?: string | null;
    messageSv?: string | null;
    messageEn?: string | null;
    severity?: string | null;
    isActive?: boolean | null;
    priority?: number | null;
    startDate?: string | null;
    endDate?: string | null;
};

export async function updateOperationalMessage(
    id: number,
    payload: PatchOperationalMessageRequest
) {
    const res = await axiosInstance.patch(`/operational-messages/${id}`,payload);
    return res.data;
}

export async function deleteOperationalMessage(id: number){
    const res = await axiosInstance.delete(`/operational-messages/${id}`);
    return res.data;
}