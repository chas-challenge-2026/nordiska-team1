import { useTranslation } from "react-i18next";
import LoginCard from "../components/login/LoginCard";

export default function LoginPage() {
    const { t } = useTranslation();

    return (
    // <main className="min-h-screen w-full bg-login-bg">
    <main className="min-h-screen w-full bg-login-bg border-t-70 border-login-bg lg:border-t-25">

        {/* ----- DESKTOP ----- */}
        {/* <div className="hidden min-h-screen w-[90vw] mx-auto grid-cols-2 items-center md:grid"> */}
        <div className="flex lg:min-h-screen flex-col justify-start px-6 py-10 sm:px-10 md:grid md:grid-cols-2 md:items-center md:px-0">



            <section className="mb-8 text-white md:mb-0 md:flex md:flex-col md:items-end md:pr-12">

                <div className="md:text-right">
                    <h1 className="font-montserrat-alternates text-4xl font-bold leading-tight md:text-7xl text-shadow-lg/90">
                        {t("login-route.title")}.
                    </h1>

                    <p className="mt-3 max-w-xl font-montserrat text-base leading-relaxed sm:text-lg md:mt-5 md:text-xl md:ml-10 text-shadow-lg/90">

                        {t("login-route.paragraph")}
                    </p>
                </div>

            </section>


            <section className="border-t-2 border-nordiska-orange pt-5 md:border-l-2 md:border-t-0 md:pl-12 md:pt-0">

                <div className="flex flex-col justify-center w-full rounded-br-[20px] rounded-bl-[20px] bg-white p-4 md:w-[360px] md:rounded-bl-none md:rounded-tr-[20px] lg:h-[480px]">


                    <LoginCard />
                </div>
            </section>
        </div>

        {/* ----- MOBILE ----- */}
        {/* <div className="flex min-h-screen flex-col px-6 py-10 sm:px-10 md:hidden">
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
        </div> */}
    </main>
    );
}
