import axiosInstance from "./axiosInstance";
import axios from "axios";
import type { User } from "../types/types";


export async function checkSession(): Promise<User | null> {
    try {
        //Authenticate
        const authRes = await axiosInstance.get<User>("/auth/me");
        const authUser = authRes.data;

        if (authUser.role === "Admin") { return authUser;}

        // Get customerinfo
        const customerRes = await axiosInstance.get<User>(`/customers/${authRes.data.id}`);

        return {
            ...customerRes.data,
            ...authUser
        };
    } catch(error) {
        // returnera inget om unauthorized
        if (axios.isAxiosError(error) && error.response?.status === 401){ return null;}
        
        // skicka resten av felen vidare
        throw error;
    }
}

export async function refreshSession(): Promise<void> {
        await axiosInstance.post<User>("auth/refresh");
        // returnerar inget för att cookien sätts direkt från backend och user sätts vid inloggning samt uppdatering av kontaktuppgifter, finns ingen mening med att denna ska returnera user - dessutom returnerar den bara token, id, name & email så det blir information loss på phone vid autorefresh?
}