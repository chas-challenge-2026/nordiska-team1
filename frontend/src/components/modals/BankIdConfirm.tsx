import { useTranslation } from "react-i18next";
import { isAxiosError } from "axios";
import BankIdQrCode from "../auth/BankIdQrCode";
import {
    ArrowRightIcon,
    DesktopIcon,
    PhoneIcon,
    QrIcon,
} from "../icons/BankIdIcons";
import { autoStartUrl, isMobileDevice } from "../../utils/bankId";
import { useBankIdConfirm } from "../../hooks/useBankIdConfirm";

type BankIdConfirmProps = {
    amountFormatted: string;
    toName: string;
    toMeta: string;
    fromName: string;
    fromMeta: string;
    date: string;
    onApprove: () => void;
    onCancel: () => void;
};

/**
 * Innehållet i BankID-bekräftelsen (renderas inuti `Modal`), endast för
 * externa mottagare. Interna överföringar hoppar över detta steg.
 * `onApprove` anropas först när överföringen signerats med test-BankID.
 */
export default function BankIdConfirm({
    amountFormatted,
    toName,
    toMeta,
    fromName,
    fromMeta,
    date,
    onApprove,
    onCancel,
}: BankIdConfirmProps) {
    const { t } = useTranslation();
    const {
        mode,
        initData,
        status,
        hintCode,
        initError,
        collectError,
        wrongUser,
        start,
    } = useBankIdConfirm(onApprove);

    function bankIdError(): string | undefined {
        if (wrongUser) return t("page-transfer.bankid.wrong-user");
        if (initError) {
            return isAxiosError(initError) && initError.response?.status === 429
                ? t("register-route.error-rate-limit")
                : t("login-route.failed");
        }
        if (collectError) {
            // 401 = den som legitimerade sig är inte kund hos oss, sessionen är orörd.
            return isAxiosError(collectError) && collectError.response?.status === 401
                ? t("page-transfer.bankid.wrong-user")
                : t("login-route.failed");
        }
        if (status === "FAILED") {
            if (hintCode === "startfailed") return t("login-route.start-failed");
            if (hintCode === "usercancel") return t("page-transfer.bankid.user-cancel");
            return t("login-route.failed");
        }
        return undefined;
    }

    const error = bankIdError();
    // BankID-appen har öppnats och väntar på att användaren godkänner
    const waitingForApp =
        status === "PENDING" && (hintCode === "started" || hintCode === "usersign");

    const options = [
        {
            key: "otherDevice" as const,
            label: t("login-route.bankid-other-device"),
            Icon: QrIcon,
        },
        {
            key: "thisDevice" as const,
            label: t("login-route.bankid-this-device"),
            Icon: isMobileDevice() ? PhoneIcon : DesktopIcon,
        },
    ];

    return (
        <div className="overflow-y-auto p-8">
            <h3 className="m-0 text-[22px] font-semibold text-dark-navy">
                {t("page-transfer.bankid.heading")}
            </h3>
            <p className="mt-2 mb-5.5 text-sm text-secondary">
                {t("page-transfer.bankid.help-text")}
            </p>

            <div className="flex flex-col gap-3 rounded-lg border border-[#E5EAF0] px-5 py-4.5">
                <div className="flex justify-between gap-4">
                    <span className="text-sm text-secondary">
                        {t("page-transfer.bankid.amount")}
                    </span>
                    <span className="text-xl font-bold text-dark-navy">
                        {amountFormatted} sek
                    </span>
                </div>
                <div className="h-px bg-[#EEF1F4]" />
                <div className="flex justify-between gap-4">
                    <span className="text-sm text-secondary">
                        {t("generic.to")}
                    </span>
                    <span className="text-right">
                        <span className="block text-[15px] font-bold text-dark-navy">
                            {toName}
                        </span>
                        <span className="block text-sm text-secondary">
                            {toMeta}
                        </span>
                    </span>
                </div>
                <div className="flex justify-between gap-4">
                    <span className="text-sm text-secondary">
                        {t("generic.from")}
                    </span>
                    <span className="text-right">
                        <span className="block text-[15px] font-bold text-dark-navy">
                            {fromName}
                        </span>
                        <span className="block text-sm text-secondary">
                            {fromMeta}
                        </span>
                    </span>
                </div>
                <div className="flex justify-between gap-4">
                    <span className="text-sm text-secondary">
                        {t("generic.date")}
                    </span>
                    <span className="text-[15px] font-bold text-dark-navy">
                        {date}
                    </span>
                </div>
            </div>

            {mode === null && (
                <div className="mt-6 flex flex-col gap-3">
                    {options.map(({ key, label, Icon }) => (
                        <button
                            key={key}
                            type="button"
                            onClick={() => start(key)}
                            className="flex w-full cursor-pointer items-center gap-4 rounded-md bg-[#F7F8FA] px-4 py-4 text-left shadow-sm transition-colors hover:bg-[#ECEFF3]"
                        >
                            <Icon className="h-6 w-6 flex-none text-dark-navy" />
                            <span className="flex-1 text-[15px] font-semibold text-dark-navy">
                                {label}
                            </span>
                            <ArrowRightIcon className="h-5 w-5 flex-none text-dark-navy" />
                        </button>
                    ))}
                </div>
            )}

            {mode !== null && (
                <div className="mt-6 flex flex-col items-center text-center">
                    {!error && !waitingForApp && mode === "otherDevice" && (
                        initData ? (
                            <BankIdQrCode
                                qrStartToken={initData.qrStartToken}
                                qrStartSecret={initData.qrStartSecret}
                            />
                        ) : (
                            <p className="text-sm text-secondary">
                                {t("login-route.qr-loading")}
                            </p>
                        )
                    )}

                    {!error && !waitingForApp && mode === "thisDevice" && (
                        <div className="flex flex-col items-center gap-3">
                            <p className="text-sm text-secondary">
                                {t("login-route.starting-app")}
                            </p>
                            {initData && (
                                <a
                                    href={autoStartUrl(initData.autoStartToken)}
                                    className="text-sm font-semibold text-nordiska-blue underline"
                                >
                                    {t("login-route.open-app-again")}
                                </a>
                            )}
                        </div>
                    )}

                    {!error && waitingForApp && (
                        <p className="text-sm font-semibold text-dark-navy">
                            {t("page-transfer.bankid.waiting-for-app")}
                        </p>
                    )}

                    {error && (
                        <div className="flex w-full flex-col items-center gap-3">
                            <p className="text-sm font-semibold text-dark-navy" role="alert">
                                {error}
                            </p>
                            {!wrongUser && (
                                <button
                                    type="button"
                                    onClick={() => start(mode)}
                                    className="w-full cursor-pointer rounded-md border-0 bg-nordiska-blue py-3 text-base font-bold text-white hover:bg-login-bg"
                                >
                                    {t("login-route.retry")}
                                </button>
                            )}
                        </div>
                    )}
                </div>
            )}

            <button
                type="button"
                onClick={onCancel}
                className="mt-3 w-full cursor-pointer border-0 bg-none py-3 text-sm font-bold text-primary-blue"
            >
                {t("generic.cancel")}
            </button>
        </div>
    );
}
