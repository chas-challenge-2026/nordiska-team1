import { useTranslation } from "react-i18next";
import { Link } from "react-router";

export default function PageNotFound() {
    const {t} = useTranslation();

    return (
        <main className="absolute inset-x-0 bottom-0 top-[70px] md:top-[80px]">
            <section aria-labelledby="page-title" className="relative z-10 flex min-h-[calc(100vh-120px)] items-center px-6 py-12 sm:px-10 lg:px-15 ">

                <div className="mx-auto flex w-full max-w-4xl flex-col gap-8 text-dark-navy lg:ml-[10%] lg:gap-10">
                    <h1 id="page-title" className="max-w-5xl font-montserrat-alternates text-3xl font-bold leading-tight sm:text-4xl md:text-5xl lg:text-6xl">
                        <span className="block">404</span>
                        {t("404-route.title")}
                    </h1>
                    <p className="max-w-3xl -mt-5 font-montserrat text-base leading-relaxed sm:text-lg lg:text-xl">
                        {t("404-route.paragraph")}
                    </p>
                    <Link 
                        to="/"
                        className="group flex min-h-14 w-[65%] max-w-xs items-center gap-4 rounded-br-[10px] rounded-bl-[10px] rounded-tr-[10px] bg-nordiska-blue px-2 py-2 font-montserrat text-white text-lg font-semibold transition-colors hover:bg-login-bg focus-visible:outline-4 focus-visible:outline-offset-4 focus-visible:outline-white sm:px-8 sm:text-xl lg:text-2xl" 
                        >
                            <img src="icons/arrow-right.svg" alt="" className='invert rotate-180 w-[24px] group-hover:animate-bounce-right ml-1 mr-1' />
                            {t("404-route.button")}
                    </Link>
                </div>
            </section>
        </main>
    );
}