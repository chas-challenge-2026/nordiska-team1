import Modal from "./modals/Modal";
import { useTranslation } from "react-i18next";

type InactivityWarningProps = {
    remainingSeconds: number;
    onStayLoggedIn: () => void;
    onLogout: () => void;
}

export default function InactivityWarning({remainingSeconds, onStayLoggedIn, onLogout}: InactivityWarningProps) {
    const {t} = useTranslation();
    const minutes = Math.floor(remainingSeconds / 60);
    const seconds = remainingSeconds % 60;
    const formattedTime = `${minutes}:${seconds.toString().padStart(2, "0")}`;

    return (

        <Modal
            title={t("inactivity-warning.doc-title")}
            onClose={onStayLoggedIn}
            widthClassName="w-[480px]"
            closeOnOverlayClick={false}
            closeOnEscape={false}>

            <div className="flex flex-col items-center px-8 py-10 text-center">
                <h2 className="text-2xl font-bold text-dark-navy"> {t("inactivity-warning.title")}</h2>
                <p className="mt-4 text-dark-navy"> {t("inactivity-warning.paragraph")} </p>

                <div
                    className="mt-6 text-5xl font-bold tabular-nums text-dark-navy"
                    aria-live="polite"
                    aria-label={`${minutes} ${t("inactivity-warning.minutes")} ${t("inactivity-warning.minutes")} ${seconds} ${t("inactivity-warning.seconds")}`}
                >
                    {formattedTime}
                </div>

                <div className="mt-8 flex w-full gap-4">
                    <button
                        type="button"
                        onClick={onLogout}
                        className="flex-1 rounded-md border-2 border-primary-blue bg-white px-3 py-2 text-sm font-semibold text-primary-blue transition-colors cursor-pointer hover:bg-nordiska-blue hover:text-white hover:border-nordiska-blue"
                    >
                        {t("generic.log-out")}
                    </button>

                    <button
                        type="button"
                        onClick={onStayLoggedIn}
                        className="flex-1 rounded-md bg-primary-blue px-3 py-2 text-sm font-semibold text-white transition-colors cursor-pointer hover:bg-nordiska-blue"
                    >
                        {t("generic.stay")}
                    </button>
                </div>
            </div>
        </Modal>
    );
};