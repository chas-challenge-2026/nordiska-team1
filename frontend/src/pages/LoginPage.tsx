import { useTranslation } from "react-i18next";
import LoginCard from "../components/login/LoginCard";

export default function LoginPage() {
    const { t } = useTranslation();

    return (
    <main className="min-h-screen w-full bg-login-bg">
        {/* ----- DESKTOP ----- */}
        <div className="hidden min-h-screen w-[90vw] mx-auto grid-cols-2 items-center md:grid">
            <section className="flex flex-col items-end pr-12">
                <div className="text-right text-white">
                    <h1 className="text-7xl font-bold font-montserrat-alternates">
                        {t("login-route.title")}.
                    </h1>

<<<<<<< HEAD
                    <p className="text-xl w-[500px] font-montserrat mt-5">
                        {t("login-route.paragraph")}
                    </p>
                </div>
            </section>
            <section className="border-l-2 border-nordiska-orange pl-12">
                <div className="w-[360px] bg-white rounded-tr-[20px] rounded-br-[20px] p-6">
                    <LoginCard />
=======
                        <p className="text-xl font-montserrat mt-5">
                            {t("login-route.paragraph")}
                        </p>
                    </div>
                </section>
                <section className="border-l-2 border-nordiska-orange pl-12">
                    <div className="w-[320px] bg-white rounded-tr-[20px] rounded-br-[20px] py-2 px-3">
                        <form onSubmit={(e) => handleSubmit(e)}>
                            <InputField name="email" type="email" label="email" placeholder="email" value={email} onChange={setEmail}/>
                            <InputField name="password" type="password" label="password" placeholder="password" value={password} onChange={setPassword}/>
                            <button type="submit" disabled={loginPending} className="cursor-pointer">{loginPending ? "Loggas in..." : "Logga in"}</button>
                        </form>
                        {loginIsError && <p className="text-red-500">{loginError.message}</p>}
                        {registerLink}

                        <section className="mt-4 border-t pt-3">
                            <p className="font-semibold text-sm mb-2 text-[#1c3844]">Logga in med BankID</p>
                            {bankIdData ? (
                                <BankIdQrCode
                                    qrStartToken={bankIdData.qrStartToken}
                                    qrStartSecret={bankIdData.qrStartSecret}
                                    autoStartToken={bankIdData.autoStartToken}
                                    onCancel={handleBankIdCancel}
                                />
                            ) : (
                                <form onSubmit={handleBankIdInit} className="flex flex-col gap-2">
                                    <InputField
                                        name="personalnum"
                                        type="text"
                                        label="Personnummer (12 siffror)"
                                        placeholder="198202116050"
                                        value={personalNum}
                                        onChange={setPersonalNum}
                                    />
                                    <button
                                        type="submit"
                                        disabled={bankIdInitPending}
                                        className="rounded bg-[#1c3844] py-2 text-sm font-semibold text-white transition hover:bg-[#235971] cursor-pointer"
                                    >
                                        {bankIdInitPending ? "Startar BankID..." : "Starta BankID"}
                                    </button>
                                    {bankIdInitIsError && (
                                        <p className="text-xs text-red-500 mt-1">{bankIdInitError.message}</p>
                                    )}
                                </form>
                            )}
                        </section>
>>>>>>> origin/develop
                </div>
            </section>
        </div>

        {/* ----- MOBILE ----- */}
        <div className="flex min-h-screen flex-col px-6 py-10 sm:px-10 md:hidden">
            <section className="flex flex-1 flex-col justify-center">
                <div className="mb-8 text-white">
                    <h1 className="font-montserrat-alternates text-5xl font-bold leading-tight sm:text-5xl">
                        {t("login-route.title")}.
                    </h1>
                    <p className="mt-3 max-w-xl font-montserrat text-base leading-relaxed sm:text-lg">
                        {t("login-route.paragraph")}
                    </p>
                </div>
                <section className="w-full border-t-2 pt-5 border-nordiska-orange">
                    <div className="w-full rounded-br-[20px] rounded-bl-[20px] bg-white p-6">
                        <LoginCard />
                    </div>
                </section>
            </section>
        </div>
    </main>
    );
}
