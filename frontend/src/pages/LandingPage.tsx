import { useTranslation } from 'react-i18next';
import { Link } from "react-router";

type pageProps = {
    inactive?: boolean
}

export default function LandingPage({ inactive = false }: pageProps) {
    const {t} = useTranslation();

    return (
        <main className="relative min-h-screen bg-nordiska-blue">
            <div
                aria-hidden="true"
                className=" absolute inset-0 bg-[url('src/assets/img/mountain_view.webp')] bg-cover bg-center opacity-65"
            />
            <section aria-labelledby="page-title" className="relative z-10 flex min-h-screen items-center">
                <div className="ml-[20%] flex max-w-4xl flex-col gap-10 text-white">
                    <h1 id="page-title" className="font-montserrat-alternates text-7xl font-bold">
                        {inactive
                            ? `${t("inactivity-route.title")}.`
                            : `${t("welcome-route.title")}.`
                        }
                    </h1>

                    <p className="w-[80%] font-montserrat text-xl">
                        {inactive
                            ? t("inactivity-route.paragraph")
                            : t("welcome-route.paragraph")
                        }
                    </p>

                    <Link
                        to="/login"
                        className="group flex h-14 w-[60%] items-center justify-between rounded-br-[10px] rounded-bl-[10px] rounded-tr-[10px] bg-nordiska-blue p-8 font-montserrat text-2xl font-bold transition-colors hover:bg-login-bg focus-visible:outline focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-white"
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
                            className="w-[36px] invert transition-transform group-hover:translate-x-2"
                        />
                    </Link>
                </div>
            </section>
        </main>
    );
};