import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { getOperationalMessage } from "../../../services/adminService";
import { useServiceAlertStore } from "../../../store/serviceAlertStore";

type LocalizedText = {
    sv: string | null;
    en: string | null;
};

type OperationalMessageData = {
    id: number;
    title: LocalizedText;
    message: LocalizedText;
    severity: string | null;
    isActive: boolean;
    priority: number;
    startDate: string | null;
    endDate: string | null;
};

export default function OperationalMessageLoader() {
    const { i18n } = useTranslation();

    useEffect(() => {
        let cancelled = false;

        async function fetchMessage() {
            try {
                const data = await getOperationalMessage();
                if (cancelled) return;

                const messages: OperationalMessageData[] = Array.isArray(data) ? data : data ? [data] : [];
                const now = Date.now();

                const activeMessages = messages
                    .filter((item) => {
                        if (!item.isActive) return false;

                        const start = item.startDate ? Date.parse(item.startDate) : -Infinity;
                        const end = item.endDate ? Date.parse(item.endDate) : Infinity;

                        return now >= start && now <= end;
                    })
                    .sort((a, b) => a.priority - b.priority);

                const selected = activeMessages.find((item) => {
                    const language = i18n.language.startsWith("sv") ? "sv" : "en";

                    return Boolean(
                        item.message?.[language]?.trim() ||
                        item.title?.[language]?.trim()
                    );
                });

                if (!selected) {
                    const store = useServiceAlertStore.getState();
                    if (store.alert && store.alert.id !== "manual") {store.closeAlert();}
                    return;
                }

                const language = i18n.language.startsWith("sv") ? "sv" : "en";
                const message = selected.message?.[language] ?? selected.title?.[language];

                if (!message?.trim()) return;

                const type = selected.severity?.toLowerCase() === "error" ? "error" : "info";

                useServiceAlertStore.getState().showAlert({
                    id: String(selected.id),
                    type,
                    message,
                });
            } catch (error) {
                console.error("Kunde inte hämta driftmeddelanden:", error);
            }
        }

        fetchMessage();

        return () => {cancelled = true;};

    }, [i18n.language]);

    return null;
}