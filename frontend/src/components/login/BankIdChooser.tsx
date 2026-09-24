import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { ArrowRightIcon, BankIdLogo, DesktopIcon, PhoneIcon } from "../icons/BankIdIcons";

type BankIdChooserProps = {
    onMobile: () => void;
    onDesktop: () => void;
    onManual: () => void;
    /** Visas bara om den skickas in, t.ex. när man kommit hit från ett annat steg. */
    onCancel?: () => void;
};

/**
 * Startvyn i inloggningskortet, byggd för att likna BankID:s egen
 * inloggning: välj BankID på mobil (QR-kod) eller på dator (autostart).
 */
export default function BankIdChooser({ onMobile, onDesktop, onManual, onCancel }: BankIdChooserProps) {
    const { t } = useTranslation();

    const options = [
        { key: "mobile", label: t("login-route.bankid-mobile"), Icon: PhoneIcon, onClick: onMobile },
        { key: "desktop", label: t("login-route.bankid-desktop"), Icon: DesktopIcon, onClick: onDesktop },
    ];

    return (
        <div className="flex flex-col items-center px-2 py-4 text-center">
            <BankIdLogo className="h-20 w-auto text-login-bg" />

            <p className="mt-5 max-w-[260px] text-sm text-secondary">
                {t("login-route.bankid-intro")}
            </p>

            <div className="mt-5 flex w-full flex-col gap-3">
                {options.map(({ key, label, Icon, onClick }) => (
                    <button
                        key={key}
                        type="button"
                        onClick={onClick}
                        className="flex w-full cursor-pointer items-center gap-4 rounded-md bg-[#F7F8FA] px-4 py-4 text-left shadow-sm transition-colors hover:bg-[#ECEFF3]"
                    >
                        <Icon className="h-6 w-6 flex-none text-dark-navy" />
                        <span className="flex-1 text-[15px] font-semibold text-dark-navy">{label}</span>
                        <ArrowRightIcon className="h-5 w-5 flex-none text-dark-navy" />
                    </button>
                ))}
            </div>

            <button
                type="button"
                onClick={onManual}
                className="mt-5 cursor-pointer text-sm font-semibold text-nordiska-blue underline"
            >
                {t("login-route.manual-link")}
            </button>

            <p className="mt-4 text-sm text-dark-navy">
                {t("register-route.not-customer")}{" "}
                <Link to="/register" className="font-bold text-nordiska-blue underline">
                    {t("register-route.link")}
                </Link>
            </p>

            {onCancel && (
                <button
                    type="button"
                    onClick={onCancel}
                    className="mt-4 cursor-pointer border-b border-secondary pb-0.5 text-sm font-semibold text-secondary"
                >
                    {t("generic.cancel")}
                </button>
            )}
        </div>
    );
}
