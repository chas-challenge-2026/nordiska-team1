import { create } from "zustand";

export type ServiceAlert = {
    id: string;
    type: "error" | "info";
    message?: string;
    date?: string;
};

type ServiceAlertState = {
    alert: ServiceAlert | null;
    showAlert: (alert: ServiceAlert) => void;
    closeAlert: () => void;
};

export const useServiceAlertStore = create<ServiceAlertState>((set) => ({
    alert: null,

    showAlert: (alert) => set({ alert }),
    closeAlert: () => set({ alert: null }),
}));