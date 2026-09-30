import axiosInstance from "./axiosInstance";
import type { User } from "../types/types";

interface BankIdInitRes {
    orderRef: string;
    autoStartToken: string;
    qrStartToken: string;
    qrStartSecret: string;
}

interface BankIdCollectUser {
    status: string;
    hintcode: string;
    customer: null | {
        id: number;
        name: string;
        email: string;
    }
}

export async function bankIdInitiate(personalNum:string):Promise<BankIdInitRes> {
    const res = await axiosInstance.post("/auth/bankid/initiate", {personalNum: personalNum});
    return res.data;
}

export async function bankIdCollect(orderRef: string):Promise<BankIdCollectUser> {
    const res = await axiosInstance.post("/auth/bankid/collect", {orderRef});
    return res.data;
}


export async function login(email: string, password: string): Promise<void> {
    await axiosInstance.post("/auth/login", { email, password });
}

export interface RegisterCustomerRequest {
    name: string;
    email: string;
    personalNum: string;
    phoneNumber?: string;
}

/** Registers a new customer. On success the backend also logs the customer in via cookie. */
export async function register(data: RegisterCustomerRequest): Promise<void> {
    await axiosInstance.post("/auth/register", data);
}

export async function logout(): Promise<void> {
    await axiosInstance.post("/auth/logout");
}

export async function checkSession(): Promise<User | null> {
    try {
        const res = await axiosInstance.get<User>("/auth/me");
        return res.data;
    } catch {
        return null;
    }
}

export async function refreshSession(): Promise<void> {
        await axiosInstance.post<User>("auth/refresh");
        // returnerar inget för att cookien sätts direkt från backend och user sätts vid inloggning samt uppdatering av kontaktuppgifter, finns ingen mening med att denna ska returnera user - dessutom returnerar den bara token, id, name & email så det blir information loss på phone vid autorefresh?
}