import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { isAxiosError } from "axios";
import { useBankIdCollect, useBankIdInitate, useLogin } from "../../hooks/useLogin";
import type { BankIdInitRes } from "../../services/authService";
import BankIdQrCode from "../auth/BankIdQrCode";
import { BankIdLogo } from "../icons/BankIdIcons";
import { autoStartUrl } from "../../utils/bankId";
import BankIdChooser from "./BankIdChooser";
import ManualLoginForm from "./ManualLoginForm";

type Step = "choose" | "otherDevice" | "thisDevice" | "manual";

/**
 * Hela inloggningskortet.
 * - "BankID på annan enhet": riktig QR-kod som skannas med BankID-appen i mobilen.
 * - "BankID på denna enhet": öppnar BankID-appen på samma enhet (dator eller mobil).
 * - Formuläret (via länken): personnummer öppnar BankID-appen låst till det
 *   personnumret (i Simulated-läget loggar det in direkt), eller e-post och lösenord.
 */
export default function LoginCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();

    const [step, setStep] = useState<Step>("choose");
    const [bankIdData, setBankIdData] = useState<BankIdInitRes | null>(null);
    const [orderRef, setOrderRef] = useState("");
    // Personnumret följer med när man trycker "Försök igen"
    const [personalNum, setPersonalNum] = useState<string>();
    const [emailError, setEmailError] = useState<string>();

    const { mutate: initiate, isPending: initPending, error: initError, reset: resetInit } = useBankIdInitate();
    const collect = useBankIdCollect(orderRef);
    const status = collect.data?.status;
    const hintCode = collect.data?.hintCode?.toLowerCase();
    const { mutate: login, isPending: loginPending } = useLogin();

    useEffect(() => {
        if (status === "COMPLETE") {
            navigate("/");
        }
    }, [status, navigate]);

    function clearOrder() {
        setBankIdData(null);
        setOrderRef("");
        resetInit();
    }

    function goTo(nextStep: Step) {
        clearOrder();
        setPersonalNum(undefined);
        setEmailError(undefined);
        setStep(nextStep);
    }

    function startBankId(nextStep: Step, pnr?: string) {
        clearOrder();
        setPersonalNum(pnr);
        setStep(nextStep);

        initiate(pnr, {
            onSuccess: (data) => {
                setBankIdData(data);
                setOrderRef(data.orderRef);
                // Allt utom QR-koden startar BankID-appen på den här enheten
                if (nextStep !== "otherDevice") {
                    window.location.href = autoStartUrl(data.autoStartToken);
                }
            },
        });
    }

    function bankIdError(): string | undefined {
        if (initError) {
            return isAxiosError(initError) && initError.response?.status === 429
                ? t("register-route.error-rate-limit")
                : t("login-route.failed");
        }
        if (collect.error) {
            // Backend svarar 401 både för okänd kund och för BankID-fel, så vi skiljer på meddelandet
            const message = isAxiosError(collect.error) ? String(collect.error.response?.data?.message ?? "") : "";
            return message.toLowerCase().includes("customer")
                ? t("login-route.not-found")
                : t("login-route.failed");
        }
        if (status === "FAILED") {
            if (hintCode === "startfailed") return t("login-route.start-failed");
            if (hintCode === "usercancel") return t("login-route.user-cancel");
            return t("login-route.failed");
        }
        return undefined;
    }

    const error = bankIdError();
    const bankIdPending = initPending || (!!orderRef && !error && status !== "COMPLETE");
    // BankID-appen har öppnats och väntar på att användaren godkänner
    const waitingForApp = status === "PENDING" && (hintCode === "started" || hintCode === "usersign");

    function handleEmailLogin(email: string, password: string) {
        setEmailError(undefined);
        login(
            { email, password },
            {
                onSuccess: () => navigate("/"),
                onError: (err) => {
                    const code = isAxiosError(err) ? err.response?.status : undefined;
                    if (code === 401) setEmailError(t("login-route.invalid-credentials"));
                    else if (code === 423) setEmailError(t("login-route.locked"));
                    else if (code === 429) setEmailError(t("register-route.error-rate-limit"));
                    else setEmailError(t("register-route.error-generic"));
                },
            }
        );
    }

    if (step === "otherDevice" || step === "thisDevice") {
        return (
            <div className="flex flex-col items-center px-2 py-4 text-center">
                <BankIdLogo className="h-14 w-auto text-login-bg" />

                {!error && !waitingForApp && step === "otherDevice" && (
                    bankIdData ? (
                        <BankIdQrCode
                            qrStartToken={bankIdData.qrStartToken}
                            qrStartSecret={bankIdData.qrStartSecret}
                        />
                    ) : (
                        <p className="mt-6 text-sm text-secondary">{t("login-route.qr-loading")}</p>
                    )
                )}

                {!error && !waitingForApp && step === "thisDevice" && (
                    <div className="mt-6 flex flex-col items-center gap-3">
                        <p className="text-sm text-secondary">{t("login-route.starting-app")}</p>
                        {bankIdData && (
                            <a
                                href={autoStartUrl(bankIdData.autoStartToken)}
                                className="text-sm font-semibold text-nordiska-blue underline"
                            >
                                {t("login-route.open-app-again")}
                            </a>
                        )}
                    </div>
                )}

                {!error && waitingForApp && (
                    <p className="mt-6 text-sm font-semibold text-dark-navy">{t("login-route.waiting-for-app")}</p>
                )}

                {error && (
                    <div className="mt-4 flex w-full flex-col items-center gap-3">
                        <p className="text-sm font-semibold text-dark-navy" role="alert">{error}</p>
                        <button
                            type="button"
                            onClick={() => startBankId(step, personalNum)}
                            className="w-full cursor-pointer rounded-md border-0 bg-nordiska-blue py-3 text-base font-bold text-white hover:bg-login-bg"
                        >
                            {t("login-route.retry")}
                        </button>
                        <button
                            type="button"
                            onClick={() => goTo("manual")}
                            className="cursor-pointer text-sm font-semibold text-nordiska-blue underline"
                        >
                            {t("login-route.manual-link")}
                        </button>
                    </div>
                )}

                <button
                    type="button"
                    onClick={() => goTo("choose")}
                    className="mt-5 cursor-pointer border-b border-secondary pb-0.5 text-sm font-semibold text-secondary"
                >
                    {t("generic.cancel")}
                </button>
            </div>
        );
    }

    if (step === "manual") {
        return (
            <ManualLoginForm
                onBankIdSubmit={(pnr) => startBankId("manual", pnr)}
                bankIdPending={bankIdPending}
                bankIdError={error}
                onEmailSubmit={handleEmailLogin}
                emailPending={loginPending}
                emailError={emailError}
                onBack={() => goTo("choose")}
            />
        );
    }

    return (
        <BankIdChooser
            onOtherDevice={() => startBankId("otherDevice")}
            onThisDevice={() => startBankId("thisDevice")}
            onManual={() => goTo("manual")}
        />
    );
}
