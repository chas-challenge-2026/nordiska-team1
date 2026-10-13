import { useServiceAlertStore } from "../store/serviceAlertStore";

export function showManualOperationalMessage(
    message: string,
    type: "info" | "error" = "info",
) {
    useServiceAlertStore.getState().showAlert({
        id: "manual",
        type,
        message,
    });
}