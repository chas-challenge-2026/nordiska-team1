import { useState } from "react";
import { useTranslation } from "react-i18next";
import InputField from "../forms/InputField";
import { isValidPersonalNum, normalizePersonalNum } from "../../utils/personalNumber";

type ManualLoginFormProps = {
    /** Anropas med ett normaliserat personnummer (12 siffror). */
    onBankIdSubmit: (personalNum: string) => void;
    bankIdPending: boolean;
    bankIdError?: string;
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
    onBankIdSubmit,
    bankIdPending,
    bankIdError,
    onEmailSubmit,
    emailPending,
    emailError,
    onBack,
}: ManualLoginFormProps) {
    const { t } = useTranslation();
    const [personalNum, setPersonalNum] = useState("");
    const [personalNumError, setPersonalNumError] = useState<string>();
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");

    function handleBankIdSubmit(e: React.SubmitEvent) {
        e.preventDefault();
        if (!isValidPersonalNum(personalNum)) {
            setPersonalNumError(t("register-route.personal-num-error"));
            return;
        }
        setPersonalNumError(undefined);
        onBankIdSubmit(normalizePersonalNum(personalNum));
    }

    function handleEmailSubmit(e: React.SubmitEvent) {
        e.preventDefault();
        onEmailSubmit(email, password);
    }

    return (
        <div className="flex flex-col gap-5 px-1 py-3">
            <form onSubmit={handleBankIdSubmit} className="flex flex-col gap-3" noValidate>
                <InputField
                    name="personalnum"
                    type="text"
                    label={t("register-route.personal-num")}
                    placeholder={t("register-route.personal-num-placeholder")}
                    value={personalNum}
                    onChange={setPersonalNum}
                    error={personalNumError}
                />
                <button type="submit" disabled={bankIdPending} className={primaryButton}>
                    {bankIdPending ? t("login-route.bankid-submitting") : t("login-route.bankid-submit")}
                </button>
                {bankIdError && <p className="text-sm text-error">{bankIdError}</p>}
            </form>

            <div className="flex items-center gap-3 text-xs text-secondary uppercase">
                <span className="h-px flex-1 bg-[#E5EAF0]" />
                {t("login-route.or-email")}
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
