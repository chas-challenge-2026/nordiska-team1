import { create } from "zustand";
import type { User } from "../types/types";

type LogoutReasons = "manual" | "inactivity";

interface UserState {
    user: User | null;
    isCheckingSession: boolean;
    loggedOut: boolean;
    loggedOutDueToInactivity: boolean;
    setUser: (user: User | null) => void;
    updateUser: (updates: Partial<User>) => void;
    setCheckingSession: (value: boolean) => void;
    logout: (reason?: LogoutReasons) => void;
}

export const useUserStore = create<UserState>((set) => ({
    user: null,
    isCheckingSession: true,
    loggedOut: false,
    loggedOutDueToInactivity: false,
    setUser: (user) => set(user ? { user, loggedOut: false, loggedOutDueToInactivity: false } : { user }),
    updateUser: (updates) => set((state) => ({ user: state.user ? { ...state.user, ...updates } : null,})),
    setCheckingSession: (value) => set({ isCheckingSession: value }),
    logout: (reason) => set({ user: null, loggedOut: true, loggedOutDueToInactivity: reason === "inactivity" }),
}));
