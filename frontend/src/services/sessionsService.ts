import axiosInstance from "./axiosInstance";
import type { User } from "../types/types";


export async function checkSession(): Promise<User | null> {
    try {
        //Authenticate
        const authRes = await axiosInstance.get<User>("/auth/me");
        // Get customerinfo
        const customerRes = await axiosInstance.get<User>(`/customers/${authRes.data.id}`);

        return {
            ...authRes.data,
            ...customerRes.data,
        };
    } catch {
        return null;
    }
}

export async function refreshSession(): Promise<void> {
        await axiosInstance.post<User>("auth/refresh");
        // returnerar inget för att cookien sätts direkt från backend och user sätts vid inloggning samt uppdatering av kontaktuppgifter, finns ingen mening med att denna ska returnera user - dessutom returnerar den bara token, id, name & email så det blir information loss på phone vid autorefresh?
}