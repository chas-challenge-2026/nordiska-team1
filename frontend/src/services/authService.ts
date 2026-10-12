import axiosInstance from "./axiosInstance";

export interface BankIdInitRes {
    orderRef: string;
    autoStartToken: string;
    qrStartToken: string;
    qrStartSecret: string;
}

interface BankIdCollectUser {
    status: string;
    hintCode: string | null;
    customer: null | {
        id: number;
        name: string;
        email: string;
    }
}

/** Utan personnummer startas en vanlig BankID-order (QR-kod eller autostart på samma enhet). */
export async function bankIdInitiate(personalNum?: string):Promise<BankIdInitRes> {
    const res = await axiosInstance.post("/auth/bankid/initiate", {personalNum: personalNum ?? ""});
    return res.data;
}

export async function bankIdCollect(orderRef: string):Promise<BankIdCollectUser> {
    const res = await axiosInstance.post("/auth/bankid/collect", {orderRef});
    return res.data;
}

export async function login(email: string, password: string): Promise<void> {
    await axiosInstance.post("/auth/login", { email, password });
}

export async function logout(): Promise<void> {
    await axiosInstance.post("/auth/logout");
}