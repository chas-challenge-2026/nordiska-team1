import { create } from "zustand";

interface User {
    id: string;
    email: string;
    role: string;
}

interface UserState {
    user: User | null;
    isCheckingSession: boolean;
    setUser: (user: User | null) => void;
    setCheckingSession: (value: boolean) => void;
    logout: () => void;
}

export const useUserStore = create<UserState>((set) => ({
    user: null,
    isCheckingSession: true,
    setUser: (user) => set({ user }),
    setCheckingSession: (value) => set({ isCheckingSession: value }),
    logout: () => set({ user: null }),
}));
