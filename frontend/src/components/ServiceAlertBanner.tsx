import { useServiceAlertStore } from "../store/serviceAlertStore";
import { useTranslation } from "react-i18next";


/*
    App.tsx:
    useServiceAlertStore.getState().showAlert({
        id: "test",
        type: "info",
        date: "2026-08-10 13:00 (esimated duration 2h)"
    })


    i18:
    "service-alert-banner": {
        "error": "Vi har för närvarande en pågående driftstörning. Vänligen försök igen senare.",
        "info": "Vi utför ett planerat underhåll: ",
        "aria-label": "Stäng meddelande om driftstörning"
    }
    "service-alert-banner": {
        "error": "We are currently experiencing a service disruption. Please try again later.",
        "info": "We are performing scheduled maintenance: ",
        "aria-label": "Close service alert"
    }

    DesktopLayout.tsx:
    <ServiceAlertBanner />
*/


export default function ServiceAlertBanner() {
    const { t } = useTranslation();

    const alert = useServiceAlertStore(
        (state) => state.alert
    );

    const closeAlert = useServiceAlertStore(
        (state) => state.closeAlert
    );

    if (!alert) {
        return null;
    }

    return (
        <div className="w-full bg-nordiska-orange px-10 py-2 flex items-center justify-between ">
            <p className="flex-1 text-center text-lg text-black font-montserrat">
                {t(`service-alert-banner.${alert.type}`)} {alert.date}
            </p>

            <button
                onClick={closeAlert}
                className="text-lg uppercase text-black cursor-pointer font-montserrat"
                aria-label={t("service-alert-banner.aria-label")}
            >
                {t("generic.close")}
            </button>
        </div>
    );
}
