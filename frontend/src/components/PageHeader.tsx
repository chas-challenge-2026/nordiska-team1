import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";
import { useEffect, useRef, useState } from "react";
import LogoutButton from "./LogoutButton";
import { useLocation } from "react-router";
import {motion} from "motion/react";

export default function PageHeader() {
    const { i18n, t } = useTranslation();
    const [languageOpen, setLanguageOpen] = useState(false);
    const languageRef = useRef<HTMLDivElement>(null);
    const location = useLocation();

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (languageRef.current && !languageRef.current.contains(event.target as Node)) {
                setLanguageOpen(false);
            }
        };
        document.addEventListener("mousedown", handleClickOutside);
        return () => { document.removeEventListener("mousedown", handleClickOutside); };
    }, []);

    const LARGE_HEADER_ROUTES = [
        "/welcome",
        "/inactive",
        "/login",
        "/logout",
    ];

    const isLargeHeader = LARGE_HEADER_ROUTES.includes(location.pathname);
    const isLogin = location.pathname === "/login";

    const headerHight = isLargeHeader ? "h-[120px]" : "h-[75px]";
    const navLinks = !isLargeHeader;
    const topPosition = isLargeHeader ? "fixed top-0" : "sticky top-0";
    const languageSelect = isLargeHeader ? "top-[115px]" : "top-[70px]";
    const logoSize = isLargeHeader ? "text-6xl" : "text-3xl";

    const headerBackground = isLogin
        ? "bg-[url('src/assets/img/winter_forrest.webp')] bg-cover bg-center"
        : "bg-nordiska-blue";

    const languageSelectBg = isLogin ? "" : "bg-nordiska-blue";
    const dotColor = isLogin ? "text-white" : "text-nordiska-orange";

    const animateHeader = ["/welcome", "/inactive", "/login", "/logout",].includes(location.pathname);
    // const animateHeader = ["/accounts", "/transactions", "/transfer",].includes(location.pathname);

    return (
        <>
        <motion.header 
            key={location.pathname}
            initial={animateHeader ? { y: "-100%" } : false}
            animate={{ y: 0 }}
            transition={{
                duration: 0.5,
                ease: "easeOut",
            }}
            className={`${topPosition} z-[1001] w-screen ${headerHight} flex items-end justify-between ${headerBackground}
            ${location.pathname === "/login" ? "bg-[url('src/assets/img/winter_forrest.webp')] bg-cover bg-center" : "bg-nordiska-blue"} gap-10 pt-0 px-5 pb-4`}>

            {/* LOGO */}
            <a
                href="/"
                className={`font-montserrat-alternates ${logoSize} text-white font-bold tracking-wider whitespace-nowrap cursor-pointer}`}
            >
                nordiska<span className={dotColor}>.</span>
            </a>

            <div className="flex gap-15">
                {/* NAVIGATION */}
                {navLinks && (
                    <nav className="flex gap-10 text-white uppercase text-[14px] tracking-[0.18em]">
                        <PageLink title={t("page-header.help-center")} route="/help" />
                        <PageLink title={t("page-header.settings")} route="/settings" />
                        <LogoutButton title={t("page-header.logout")} />
                    </nav>
                )}

                {/* LANGUAGE */}
                <div ref={languageRef}>
                    <button
                        onClick={() => setLanguageOpen(!languageOpen)}
                        className="flex cursor-pointer items-center text-[14px] uppercase tracking-[0.18em] text-white font-light"
                    >
                        <img
                            className="h-[14px] w-[19px] invert"
                            src="icons/lang-icon.svg"
                            alt="globe icon"
                        />
                        {i18n.language}
                    </button>

                    {languageOpen && (
                        <div className={`fixed ${languageSelect}  w-25 h-12 flex items-center justify-center right-0 ${languageSelectBg} rounded-bl-2xl font-regular`}>
                            <button
                                onClick={() => {
                                    i18n.changeLanguage(i18n.language === "sv" ? "en" : "sv");
                                    setLanguageOpen(false);
                                }}
                                className=" px-3 py-2 ml-[7px] uppercase font-montserrat tracking-[0.18em] text-white cursor-pointer text-[14px]"
                            >
                                {i18n.language === "sv" ? "English" : "Svenska"}
                            </button>
                        </div>
                    )}
                </div>
            </div>
        </motion.header>
        </>
    )
}
