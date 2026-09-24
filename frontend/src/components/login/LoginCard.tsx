import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { isAxiosError } from "axios";
import { useBankIdCollect, useBankIdInitate, useLogin } from "../../hooks/useLogin";
import { BankIdLogo } from "../icons/BankIdIcons";
import BankIdChooser from "./BankIdChooser";
import ManualLoginForm from "./ManualLoginForm";
import MockQrCode from "./MockQrCode";

type Step = "choose" | "mobile" | "manual";

/**
 * Hela inloggningskortet. BankID-delen är mock: QR-koden gör ingenting och
 * "BankID på dator" leder till formuläret, där man loggar in med
 * personnummer eller e-post och lösenord precis som tidigare.
 */
export default function LoginCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();

    const [step, setStep] = useState<Step>("choose");
    const [orderRef, setOrderRef] = useState("");
    const [emailError, setEmailError] = useState<string>();

    const { mutate: initiate, isPending: initPending, error: initError, reset: resetInit } = useBankIdInitate();
    const collect = useBankIdCollect(orderRef);
    const status = collect.data?.status;
    const { mutate: login, isPending: loginPending } = useLogin();

    useEffect(() => {
        if (status === "COMPLETE") {
            navigate("/");
        }
    }, [status, navigate]);

    function goTo(nextStep: Step) {
        setOrderRef("");
        resetInit();
        setEmailError(undefined);
        setStep(nextStep);
    }

    function handlePersonalNumLogin(personalNum: string) {
        setOrderRef("");
        initiate(personalNum, {
            onSuccess: (data) => setOrderRef(data.orderRef),
        });
    }

    // Samma flöde som tidigare: initiate med personnummer och sedan pollas collect tills det är klart
    const personalNumPending =
        initPending || (!!orderRef && !collect.error && status !== "FAILED" && status !== "COMPLETE");

    function personalNumError(): string | undefined {
        if (initError) {
            return isAxiosError(initError) && initError.response?.status === 429
                ? t("register-route.error-rate-limit")
                : t("register-route.error-generic");
        }
        if (collect.error) {
            // Backend svarar 401 både för okänd kund och för andra fel, så vi skiljer på meddelandet
            const message = isAxiosError(collect.error) ? String(collect.error.response?.data?.message ?? "") : "";
            return message.toLowerCase().includes("customer")
                ? t("login-route.not-found")
                : t("register-route.error-generic");
        }
        if (status === "FAILED") {
            return t("register-route.error-generic");
        }
        return undefined;
    }

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

    if (step === "mobile") {
        return (
            <div className="flex flex-col items-center px-2 py-4 text-center">
                <BankIdLogo className="h-14 w-auto text-login-bg" />
                <div className="mt-4 rounded-lg border border-[#E5EAF0] bg-white p-2 shadow-sm">
                    <MockQrCode className="h-44 w-44 text-dark-navy" />
                </div>
                <p className="mt-3 text-sm text-secondary">{t("login-route.qr-help")}</p>
                <button
                    type="button"
                    onClick={() => goTo("manual")}
                    className="mt-5 cursor-pointer text-sm font-semibold text-nordiska-blue underline"
                >
                    {t("login-route.manual-link")}
                </button>
                <button
                    type="button"
                    onClick={() => goTo("choose")}
                    className="mt-4 cursor-pointer border-b border-secondary pb-0.5 text-sm font-semibold text-secondary"
                >
                    {t("generic.cancel")}
                </button>
            </div>
        );
    }

    if (step === "manual") {
        return (
            <ManualLoginForm
                onBankIdSubmit={handlePersonalNumLogin}
                bankIdPending={personalNumPending}
                bankIdError={personalNumError()}
                onEmailSubmit={handleEmailLogin}
                emailPending={loginPending}
                emailError={emailError}
                onBack={() => goTo("choose")}
            />
        );
    }

    return (
        <BankIdChooser
            onMobile={() => goTo("mobile")}
            onDesktop={() => goTo("manual")}
            onManual={() => goTo("manual")}
        />
    );
}
