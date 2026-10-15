import { useServiceAlertStore } from "../store/serviceAlertStore";
import { useTranslation } from "react-i18next";

type ServiceAlertBannerProps = {margin?: boolean; hidden?: boolean;}
export default function ServiceAlertBanner({margin = false, hidden= false}: ServiceAlertBannerProps) {
    const { t } = useTranslation();
    
    const alert = useServiceAlertStore((state) => state.alert);
    const closeAlert = useServiceAlertStore((state) => state.closeAlert);

    if (!alert) {return null;}

    const topMargin = margin ? "top-[75px]  md:top-[120px]" : "top-[60px] md:top-[75px]"
    const hideBanner = hidden ? "hidden" : "";

    return (
        <div 
            className={`${hideBanner} ${topMargin} sticky z-50 flex w-full items-center justify-between gap-4 bg-nordiska-orange py-1 md:px-4 sm:py-2`}>
            <p className="flex-1 text-center text-xs md:text-sm lg:text-base text-black font-montserrat">
                {alert.message
                    ? alert.message
                    : `${t(`service-alert-banner.${alert.type}`)} ${alert.date ?? ""}`}
            </p>

            <button
                onClick={closeAlert}
                className=" text-sm sm:text-lg mr-2 md:mr-0 uppercase text-black cursor-pointer font-montserrat"
                aria-label={t("service-alert-banner.aria-label")}
            >
              ✕
            </button>
        </div>
    );
}
