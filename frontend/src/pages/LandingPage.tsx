import { useTranslation } from 'react-i18next';
import { Link } from "react-router";

type pageProps = {
    inactive?: boolean
}

export default function LandingPage({ inactive = false }: pageProps) {
    const {t} = useTranslation();

    return (
<main className="relative h-screen overflow-hidden  border-nordiska-blue bg-nordiska-blue">
    {/* ----- BAKGRUND ----- */}
    <div
        aria-hidden="true"
        className="absolute inset-x-0 bottom-0 top-[70px] bg-[url('src/assets/img/mountain_view.webp')] bg-cover bg-center opacity-65 md:top-[120px]"
    />
            {/* ----- INNEHÅLL ----- */}
            <section aria-labelledby="page-title" className="relative z-10 flex min-h-[calc(100vh-120px)] items-center px-6 py-12 sm:px-10 lg:px-15 mt-[70px]">
                <div className="mx-auto flex w-full max-w-4xl flex-col gap-8 text-white lg:ml-[10%] lg:gap-10">

                    <h1 id="page-title" className="max-w-5xl font-montserrat-alternates text-4xl font-bold leading-tight sm:text-5xl md:text-6xl lg:text-7xl">
                        {inactive
                            ? `${t("inactivity-route.title")}.`
                            : `${t("welcome-route.title")}.`
                        }
                    </h1>

                    <p className="max-w-3xl font-montserrat text-base leading-relaxed sm:text-lg lg:text-xl">
                        {inactive
                            ? t("inactivity-route.paragraph")
                            : t("welcome-route.paragraph")
                        }
                    </p>

                    <Link
                        to="/login"
                        className="group flex min-h-14 w-full max-w-xl items-center justify-between gap-4 rounded-br-[10px] rounded-bl-[10px] rounded-tr-[10px] bg-nordiska-blue px-6 py-4 font-montserrat text-lg font-bold transition-colors hover:bg-login-bg focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-white sm:px-8 sm:text-xl lg:text-2xl"
                    >
                        <span>
                            {inactive
                                ? t("inactivity-route.button")
                                : t("welcome-route.button")
                            }
                        </span>
                        <img
                            aria-hidden="true"
                            src="/icons/arrow-right.svg"
                            alt=""
                            className="h-7 w-7 shrink-0 invert transition-transform group-hover:translate-x-2"
                        />
                    </Link>
                </div>
            </section>
        </main>
    );
};