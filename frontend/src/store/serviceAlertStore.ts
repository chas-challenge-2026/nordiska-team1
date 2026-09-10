import { create } from "zustand";

type ServiceAlert = 
    | {
        id: string;
        type: "error";
        date?: string;
    }
    | {
        id: string;
        type: "info";
        date: string;
    };


type ServiceAlertState = {
    alert: ServiceAlert | null;
    showAlert: (alert: ServiceAlert) => void;
    closeAlert: () => void;
};

export const useServiceAlertStore = create<ServiceAlertState>((set) => ({
    alert: null,

    showAlert: (alert) => {
        set({ alert });
    },

    closeAlert: () => {
        set({ alert: null });
    },
}));