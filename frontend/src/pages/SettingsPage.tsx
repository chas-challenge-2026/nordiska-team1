import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router";
import Collapsible from "../components/Collapsible";
import { UpdateCustomerForm } from "../components/forms/UpdateCustomerForm";
import { useTranslation } from "react-i18next";
import { useUserStore } from "../store/userStore";

export default function SettingsPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const user = useUserStore((state) => state.user);

    const [openField, setOpenField] = useState<"email" | "phone" | null>(null);

    const savedEmail = user ? user.email : "";
    const savedPhone = user ? user.phone : "";
    const [message, setMessage] = useState("");
    const [messageType, setMessageType] = useState<"error" | "success" | "">("");

    const handleError = (message: string) => {
        setMessage(message);
        setMessageType("error");
    };

    const handleSuccess = (message: string) => {
        setMessage(message);
        setMessageType("success");
    };

    useEffect(() => {
        if (!message) return;


        const timer = setTimeout(() => {
            setMessage("");
            setMessageType("");
        }, 3700);

        return () => clearTimeout(timer);
    }, [message]);

    return (
        <main className="relative min-h-[calc(100vh-75px)] bg-dark-navy px-4 py-6 text-dark-navy sm:px-6 lg:px-10">

            {/* ----- BAKGRUND ----- */}
            <div className="fixed inset-0 z-0 bg-dark-navy border-t-50 border-dark-navy">
                <div
                    aria-hidden="true"
                    className="absolute inset-0 bg-[url('/images/winter_forrest.webp')] bg-cover bg-center opacity-65"
                />
            </div>

            {/* ----- YVKORT ----- */}
            <div className="relative z-10 mx-auto grid min-h-[calc(100vh-123px)] w-full max-w-6xl grid-cols-1 bg-white p-6 font-montserrat sm:p-10 lg:grid-cols-2 lg:p-15">

                <button
                    type="button"
                    onClick={ () => navigate("/")}
                    className="absolute right-4 top-4 z-10 hidden min-h-11 items-center gap-2 rounded-sm px-2 font-montserrat text-sm font-semibold uppercase text-primary-blue cursor-pointer transition-colors hover:text-nordiska-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-dark-navy md:flex"
                >
                    <span>{t("settings-route.close")}</span>

                    <span
                        aria-hidden="true"
                        className="text-xl leading-none"
                    >
                        ×
                    </span>
                </button>

                {/* ----- INSTÄLLNINGAR ----- */}
                <section
                    aria-labelledby="settings-title"
                    className="flex flex-col pt-12 lg:pr-12 lg:pt-10"
                >
                    <h1
                        id="settings-title"
                        className="-mt-5 mb-8 border-b-2 border-nordiska-orange pb-2 text-2xl font-bold uppercase tracking-wider sm:text-3xl"
                    >
                        {t("settings-route.page-title")}
                    </h1>

                    <div className="space-y-4">
                        <Collapsible
                            title={t("forms.email")}
                            preview={savedEmail}
                            label={
                                savedEmail
                                    ? t("generic.edit")
                                    : t("generic.add")
                            }
                            isOpen={openField === "email"}
                            onOpenChange={(open) =>
                                setOpenField(open ? "email" : null)
                            }
                        >
                            {(close) => <UpdateCustomerForm type="email" onClose={close} onError={handleError} onSuccess={handleSuccess}/>}
                        </Collapsible>

                        <Collapsible
                            title={t("forms.phone")}
                            preview={savedPhone}
                            label={
                                savedPhone
                                    ? t("generic.edit")
                                    : t("generic.add")
                            }
                            isOpen={openField === "phone"}
                            onOpenChange={(open) =>
                                setOpenField(open ? "phone" : null)
                            }
                        >
                            {(close) => <UpdateCustomerForm type="phone" onClose={close} onError={handleError} onSuccess={handleSuccess}/>}
                        </Collapsible>

                        {message && (
                            <div
                                role="alert"
                                className={`mt-2 md:mt-15 z-50 animate-[toast-in_0.3s_ease-out] rounded-lg md:rounded-xl md:px-4 py-1 md:py-2 font-normal text-center text-xs md:text-base text-white shadow-lg ${messageType === "success" ? "bg-success" : "bg-error" }`}
                            >
                                {message}
                            </div>
                         )}
                    </div>
                </section>

                {/* ----- HJÄLP ----- */}
                <section
                    aria-labelledby="security-title"
                    className="mt-10 flex flex-col pt-8 lg:mt-0 lg:justify-center lg:border-l lg:border-dark-navy lg:pl-10 lg:pt-0"
                >
                    <h2
                        id="security-title"
                        className="mb-1 text-lg font-semibold sm:text-2xl sm:mb-3"
                    >
                        {t("settings-route.safe-title")}
                    </h2>

                    <p className="text-base leading-relaxed">
                        {t("settings-route.safe-paragraph")}
                    </p>

                    <img
                        src="images/mountain_view.webp"
                        alt=""
                        aria-hidden="true"
                        className="my-8 h-auto w-full object-cover"
                    />

                    <h2 className="text-right text-m font-semibold sm:text-xl sm:mb-3">
                        {t("settings-route.help-title")}
                    </h2>

                    <Link
                        to="/help"
                        className="group flex min-h-11 items-center justify-end rounded-sm font-semibold text-primary-blue transition-colors hover:text-nordiska-blue focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-dark-navy"
                    >
                        <span>{t("settings-route.help-link")}</span>

                        <svg
                            aria-hidden="true"
                            className="ml-2 mr-1 h-3 w-3 transition-transform group-hover:animate-bounce-right"
                            viewBox="0 0 12 12"
                            fill="none"
                        >
                            <path
                                d="M1 6H11M7 2L11 6L7 10"
                                stroke="currentColor"
                                strokeWidth="2"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                    </Link>
                </section>
            </div>
        </main>
    );
}
