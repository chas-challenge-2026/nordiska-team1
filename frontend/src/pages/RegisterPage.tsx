import { useState } from "react";
import type { SubmitEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { isAxiosError } from "axios";
import InputField from "../components/forms/InputField";
import { useRegister } from "../hooks/useLogin";
import { isValidPersonalNum, normalizePersonalNum } from "../utils/personalNumber";

type FieldErrors = Partial<Record<"name" | "email" | "personalNum" | "phoneNumber", string>>;

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Pulls field errors out of a backend ProblemDetails response ({ errors: { Field: [msg] } }). */
function getBackendFieldErrors(data: unknown): FieldErrors {
    const errors = (data as { errors?: Record<string, string[]> } | undefined)?.errors;
    if (!errors) return {};

    const result: FieldErrors = {};
    for (const [key, messages] of Object.entries(errors)) {
        const field = (["name", "email", "personalNum", "phoneNumber"] as const)
            .find((f) => f.toLowerCase() === key.toLowerCase());
        if (field && messages[0]) result[field] = messages[0];
    }
    return result;
}

export default function RegisterPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { mutate: register, isPending } = useRegister();

    const [name, setName] = useState("");
    const [email, setEmail] = useState("");
    const [personalNum, setPersonalNum] = useState("");
    const [phoneNumber, setPhoneNumber] = useState("");

    const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
    const [formError, setFormError] = useState("");

    function validate(): FieldErrors {
        const errors: FieldErrors = {};
        const trimmedName = name.trim();
        const trimmedEmail = email.trim();
        const trimmedPhone = phoneNumber.trim();

        if (!trimmedName || trimmedName.length > 200) errors.name = t("register-route.name-error");
        if (!EMAIL_REGEX.test(trimmedEmail) || trimmedEmail.length > 320) errors.email = t("register-route.email-error");
        if (!isValidPersonalNum(personalNum)) errors.personalNum = t("register-route.personal-num-error");
        if (trimmedPhone && !/^07\d{8}$/.test(trimmedPhone)) errors.phoneNumber = t("forms.phone-error");

        return errors;
    }

    function handleSubmit(e: SubmitEvent<HTMLFormElement>) {
        e.preventDefault();
        setFormError("");

        const errors = validate();
        setFieldErrors(errors);
        if (Object.keys(errors).length > 0) return;

        const trimmedPhone = phoneNumber.trim();

        register(
            {
                name: name.trim(),
                email: email.trim(),
                personalNum: normalizePersonalNum(personalNum),
                ...(trimmedPhone && { phoneNumber: trimmedPhone }),
            },
            {
                onSuccess: (result) => {
                    if (!result.accountCreated) {
                        // Kunden är registrerad och inloggad, men sparkontot
                        // kunde inte skapas automatiskt - vi navigerar ändå
                        // vidare och låter kunden skapa kontot manuellt.
                        console.warn("Registrering lyckades, men det automatiska sparkontot kunde inte skapas.");
                    }
                    navigate("/");
                },
                onError: (err) => {
                    if (!isAxiosError(err)) {
                        setFormError(t("register-route.error-generic"));
                        return;
                    }

                    const status = err.response?.status;

                    if (status === 400) {
                        const backendErrors = getBackendFieldErrors(err.response?.data);
                        if (Object.keys(backendErrors).length > 0) {
                            setFieldErrors(backendErrors);
                            return;
                        }
                        // Backend skickar i dag ingen 409 för dubbletter, bara 400 med
                        // ett enkelt { message }-fel (t.ex. "e-post finns redan"). Utan
                        // fältfel att peka ut är det enda 400-scenariot vi vet om just
                        // nu en dubblett, så vi visar samma meddelande som 409 hade fått.
                        setFormError(t("register-route.error-conflict"));
                        return;
                    }

                    if (status === 409) {
                        setFormError(t("register-route.error-conflict"));
                    } else if (status === 429) {
                        setFormError(t("register-route.error-rate-limit"));
                    } else if (status === undefined || status >= 500) {
                        // Serverfel eller inget svar alls (nätverksfel, backend nere) -
                        // samma hantering som övriga sidor.
                        navigate("/error-500");
                    } else {
                        setFormError(t("register-route.error-generic"));
                    }
                },
            }
        );
    }

    return (
        <main className="min-h-screen w-full bg-login-bg">
            <div className="flex min-h-screen flex-col justify-center px-6 py-10 sm:px-10 md:grid md:grid-cols-2 md:items-center md:px-0">
                {/* ----- RUBRIK ----- */}
                <section aria-labelledby="page-title" className="mb-8 text-white md:mb-0 md:flex md:flex-col md:items-end md:pr-12">
                    <div className="md:text-right">
                        <h1 id="page-title" className="font-montserrat-alternates text-5xl font-bold leading-tight md:text-7xl">
                            {t("register-route.title")}.
                        </h1>
                        <p className="mt-3 max-w-xl font-montserrat text-base leading-relaxed sm:text-lg md:mt-5 md:w-[500px] md:text-xl">
                            {t("register-route.paragraph")}
                        </p>
                    </div>
                </section>

                {/* ----- FORMULÄR ----- */}
                <section className="border-t-2 border-nordiska-orange pt-5 md:border-l-2 md:border-t-0 md:pl-12 md:pt-0">
                    <div className="w-full rounded-br-[20px] rounded-bl-[20px] bg-white p-6 md:w-[360px] md:rounded-bl-none md:rounded-tr-[20px]">
                        <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4 font-montserrat">
                            <InputField
                                name="name"
                                type="text"
                                label={t("register-route.name")}
                                placeholder={t("forms.placeholder") + t("register-route.name")}
                                value={name}
                                required
                                onChange={setName}
                                error={fieldErrors.name}
                            />
                            <InputField
                                name="email"
                                type="email"
                                label={t("forms.email")}
                                placeholder={t("forms.placeholder") + t("forms.email")}
                                value={email}
                                required
                                onChange={setEmail}
                                error={fieldErrors.email}
                            />
                            <InputField
                                name="personalNum"
                                type="text"
                                label={t("register-route.personal-num")}
                                placeholder={t("register-route.personal-num-placeholder")}
                                value={personalNum}
                                required
                                onChange={setPersonalNum}
                                error={fieldErrors.personalNum}
                            />
                            <InputField
                                name="phoneNumber"
                                type="tel"
                                label={t("forms.phone")}
                                placeholder="07xxxxxxxx"
                                value={phoneNumber}
                                onChange={setPhoneNumber}
                                error={fieldErrors.phoneNumber}
                            />

                            {formError && (
                                <p role="alert" className="text-sm text-error">{formError}</p>
                            )}

                            <button
                                type="submit"
                                disabled={isPending}
                                className="min-h-12 cursor-pointer rounded-br-[10px] rounded-bl-[10px] rounded-tr-[10px] bg-nordiska-blue px-6 py-3 font-montserrat font-bold text-white transition-colors hover:bg-login-bg focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-nordiska-blue disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                {isPending ? t("register-route.submitting") : t("register-route.submit")}
                            </button>
                        </form>

                        <p className="mt-5 text-sm text-dark-navy">
                            {t("register-route.already-customer")}{" "}
                            <Link to="/login" className="font-bold text-nordiska-blue underline">
                                {t("register-route.login-link")}
                            </Link>
                        </p>
                    </div>
                </section>
            </div>
        </main>
    );
}
