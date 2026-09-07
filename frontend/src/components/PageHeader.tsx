import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";
import { useEffect, useRef, useState } from "react";

type PageHeaderProps = {
    navLinks?: boolean;
    login?: boolean;
    fixedPos?: boolean;
};

/**
 * Header för applikationen.
 * navLinks styr om navigationen visas.
 * login ändrar headerns styling för login-sidan.
 */
export default function PageHeader({navLinks = true, login = false, fixedPos = false}: PageHeaderProps) {
    const {i18n, t} = useTranslation();
    const [languageOpen, setLanguageOpen] = useState(false);
    const languageRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (languageRef.current && !languageRef.current.contains(event.target as Node)) {
                setLanguageOpen(false);
            }
        };
        document.addEventListener("mousedown", handleClickOutside);
        return () => {document.removeEventListener("mousedown", handleClickOutside);};
    }, []);

    return (
        <header className={`${fixedPos ? "fixed top-0 z-[1001]" : ""} w-screen h-[100px] flex items-end justify-between ${login ? "bg-[url('src/assets/img/winter_forrest.webp')] bg-cover bg-center" : "bg-nordiska-blue"} gap-10 pt-0 px-10 pb-5`}>
            
            {/* LOGO */}
            <a
                href="/"
                className="font-montserrat-alternates text-6xl text-white font-bold tracking-wider whitespace-nowrap cursor-pointer"
            >
                nordiska<span className={login ? "text-white": "text-nordiska-orange"}>.</span>
            </a>

            <div className="flex gap-15">
                {/* NAVIGATION */}
                {navLinks && (
                    <nav className="flex gap-10 text-white uppercase tracking-[0.18em]">
                        <PageLink title={t("page-header.help-center")} route="/help"/>
                        <PageLink title={t("page-header.settings")} route="/settings" />
                        <PageLink title={t("page-header.logout")} route="/logout" />
                    </nav>
                )}

                {/* LANGUAGE */}
                <div ref={languageRef}>
                    <button
                        onClick={() => setLanguageOpen(!languageOpen)}
                        className="flex cursor-pointer items-center text-lg uppercase tracking-[0.18em] text-white font-light"
                    >
                        <img
                            className="h-[19px] w-[24px] invert"
                            src="icons/lang-icon.svg"
                            alt=""
                        />
                        {i18n.language}
                    </button>

                    {languageOpen && (
                        <div className={`fixed top-[90px] w-35 h-15 flex items-center justify-center right-0 ${login ? "" : "bg-nordiska-blue"} rounded-bl-2xl font-regular`}>
                            <button
                                onClick={() => {
                                    i18n.changeLanguage(i18n.language === "sv" ? "en" : "sv");
                                    setLanguageOpen(false);
                                }}
                                className=" px-3 py-2 uppercase font-montserrat tracking-[0.18em] text-white cursor-pointer"
                            >
                                {i18n.language === "sv" ? "English" : "Svenska"}
                            </button>
                        </div>
                    )}
                </div>
            </div>
        </header>
    )
}