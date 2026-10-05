import { useState } from "react";
import { useTranslation } from "react-i18next";
import InputField from "../forms/InputField";

type ManualLoginFormProps = {
    onEmailSubmit: (email: string, password: string) => void;
    emailPending: boolean;
    emailError?: string;
    onBack: () => void;
};

const primaryButton =
    "w-full cursor-pointer rounded-md border-0 bg-nordiska-blue py-3 text-base font-bold text-white hover:bg-login-bg disabled:cursor-not-allowed disabled:opacity-60";

/**
 * Inloggning utan QR/autostart: BankID med personnummer, eller e-post och
 * lösenord. Formuläret äger bara sina fält - anropen görs av föräldern.
 */
export default function ManualLoginForm({
    onEmailSubmit,
    emailPending,
    emailError,
    onBack,
}: ManualLoginFormProps) {
    const { t } = useTranslation();
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");


    function handleEmailSubmit(e: React.SubmitEvent) {
        e.preventDefault();
        onEmailSubmit(email, password);
    }

    return (
        <div className="flex flex-col gap-5 px-1 py-3">

            <div className="flex items-center gap-3 text-xs text-secondary uppercase">
                <span className="h-px flex-1 bg-[#E5EAF0]" />
                {t("login-route.email")}
                <span className="h-px flex-1 bg-[#E5EAF0]" />
            </div>

            <form onSubmit={handleEmailSubmit} className="flex flex-col gap-3">
                <InputField
                    name="email"
                    type="email"
                    label={t("login-route.email")}
                    placeholder={t("login-route.email-placeholder")}
                    value={email}
                    onChange={setEmail}
                    required
                />
                <InputField
                    name="password"
                    type="password"
                    label={t("login-route.password")}
                    placeholder={t("login-route.password")}
                    value={password}
                    onChange={setPassword}
                    required
                />
                <button type="submit" disabled={emailPending} className={primaryButton}>
                    {emailPending ? t("login-route.submitting") : t("login-route.submit")}
                </button>
                {emailError && <p className="text-sm text-error">{emailError}</p>}
            </form>

            <button
                type="button"
                onClick={onBack}
                className="cursor-pointer self-center border-b border-secondary pb-0.5 text-sm font-semibold text-secondary"
            >
                {t("login-route.back")}
            </button>
        </div>
    );
}
