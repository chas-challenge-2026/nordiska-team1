import axiosInstance from "./axiosInstance";

interface User {
    id: string;
    email: string;
    role: string;
}

export async function login(email: string, password: string): Promise<void> {
    await axiosInstance.post("/auth/login", { email, password });
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
