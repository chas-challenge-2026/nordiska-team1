import { useTranslation } from "react-i18next";
import InputField from "../components/InputField";
import { useState } from "react";
import { useLogin } from "../hooks/useLogin";
import { useNavigate } from "react-router";

export default function LoginPage() {
    const { t } = useTranslation();
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const {mutate, isPending, isError, error } = useLogin();
    const navigate = useNavigate();

    function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        mutate(
            {email, password},
            {
                onSuccess: () => navigate("/"),
                onError: (err) => console.error(err),
            }
        );
    }

    return (
    <main className="min-h-screen w-full bg-login-bg">
        {/* ----- DESKTOP ----- */}
        <div className="hidden min-h-screen grid-cols-2 items-center md:grid">
            <section className="flex flex-col items-end pr-12">
                <div className="text-right text-white">
                    <h1 className="text-7xl font-bold font-montserrat-alternates">
                        {t("login-route.title")}.
                    </h1>

                        <p className="text-xl w-[500px] font-montserrat mt-5">
                            {t("login-route.paragraph")}
                        </p>
                    </div>
                </section>
                <section className="border-l-2 border-nordiska-orange pl-12">
                    <div className="h-[30vh] w-[320px] bg-white rounded-tr-[20px] rounded-br-[20px]">
                        <form onSubmit={(e) => handleSubmit(e)}>
                            <InputField name="email" type="email" label="email" placeholder="email" value={email} onChange={setEmail}/>
                            <InputField name="password" type="password" label="password" placeholder="password" value={password} onChange={setPassword}/>
                            <button type="submit" disabled={isPending}>{isPending ? "loggas in..." : "logga in"}</button>
                        </form>
                        {isError && <p className="text-red-500">{error.message}</p>}

                    <p>Inlogg med BankID här</p>
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
                        <form onSubmit={handleSubmit} className="flex flex-col gap-4" > 
                            <InputField 
                                name="email" 
                                type="email" 
                                label="email" 
                                placeholder="email" 
                                value={email} 
                                onChange={setEmail} /> 
                            <InputField 
                                name="password" 
                                type="password" 
                                label="password" 
                                placeholder="password" 
                                value={password} 
                                onChange={setPassword} /> 
                            <button type="submit" disabled={isPending} > 
                                {isPending 
                                    ? "loggas in..." 
                                    : "logga in"} 
                            </button> 
                        </form> 
                        
                        {isError && ( 
                            <p className="mt-3 text-red-500"> 
                                {error.message} 
                            </p> 
                        )} 
                        
                        <p className="mt-5"> Inlogg med BankID här </p> 
                    </div>
                </section>
            </section>
        </div>
    </main>
    );
}
