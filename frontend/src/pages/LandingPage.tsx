import { useTranslation } from 'react-i18next';
import { Link } from "react-router";
import { useEffect } from "react";

type pageProps = {
    inactive?: boolean
    loggedOut?: boolean
}

export default function LandingPage({ inactive = false, loggedOut = false }: pageProps) {
    const { t } = useTranslation();

    useEffect(() => {
        if (inactive || loggedOut) {document.title=t("loggedout-route.doc-title")} 
        else {document.title="Nordiska."}
    },[t, loggedOut, inactive])

    return (
    <main className="relative flex min-h-0 flex-1 flex-col overflow-hidden bg-dark-navy/10">
        {/* ----- BAKGRUND ----- */}
        <div aria-hidden="true" className="absolute inset-0 bg-[url('/images/mountain_view.webp')] bg-cover bg-center opacity-80"/>
        {/* ----- INNEHÅLL ----- */}
        <section aria-labelledby="page-title" className="relative z-10 flex min-h-0 flex-1 items-center px-6 py-6 sm:px-10 lg:px-15 -mt-15">
            <div className="mx-auto flex w-full max-w-4xl flex-col gap-8 text-white lg:ml-[10%] lg:gap-10">

                <h1 id="page-title" className="max-w-5xl font-montserrat-alternates text-4xl text-shadow-lg/90 font-bold leading-tight sm:text-5xl md:text-6xl lg:text-7xl">
                    {loggedOut
                        ? `${t("loggedout-route.title")}`
                        : inactive
                            ? `${t("inactivity-route.title")}.`
                            : `${t("welcome-route.title")}.`
                    }
                </h1>
                <p className="max-w-3xl font-montserrat text-base leading-relaxed text-shadow-lg/90 sm:text-lg lg:text-xl -mt-8">
                    {loggedOut
                        ? `${t("loggedout-route.paragraph")}`
                        : inactive
                            ? t("inactivity-route.paragraph")
                            : t("welcome-route.paragraph")
                    }
                </p>
                   
                <Link to="/login" className="group flex min-h-14 w-full max-w-sm items-center justify-between gap-4 rounded-br-[10px] rounded-bl-[10px] rounded-tr-[10px] shadow-xl/30 bg-white px-6 py-4 font-montserrat text-lg font-bold transition-colors hover:bg-primary-hover focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-white sm:px-8 sm:text-xl lg:text-2xl ">
                    <span className='text-primary-blue group-hover:text-white shadow-0 text-shadow-0'>{inactive || loggedOut ? t("inactivity-route.button") : t("welcome-route.button")}</span>
                    <span aria-hidden="true" className={`block size-7 mask-contain mask-center mask-no-repeat bg-primary-blue group-hover:bg-white transition transition-transform group-hover:translate-x-2`} style={{maskImage: `url('/icons/arrow-right.svg')`,}}/>
                </Link>
               
                {!loggedOut && !inactive && (
                    <Link to="/register" className="group -mt-4 flex min-h-14 w-full max-w-sm items-center justify-between gap-4 rounded-br-[10px] rounded-bl-[10px] rounded-tr-[10px] border-2 border-white px-6 py-4 backdrop-blur-sm font-montserrat text-lg font-bold transition-colors hover:bg-white/10 focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-white sm:px-8 sm:text-xl lg:-mt-6 lg:text-2xl">
                        <span>{t("welcome-route.register-button")}</span>
                        <span aria-hidden="true" className={`block size-7 mask-contain mask-center mask-no-repeat bg-white transition-transform group-hover:translate-x-2`} style={{maskImage: `url('/icons/arrow-right.svg')`,}}/>
                    </Link>
                )}
            </div>
        </section>
    </main>
    );
};
