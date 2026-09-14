import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";
import { useEffect, useRef, useState } from "react";
import LogoutButton from "./LogoutButton";
import { useLocation, Link } from "react-router";
import {motion, AnimatePresence} from "motion/react";

export default function PageHeader() {
    const { i18n, t } = useTranslation();
    const [languageOpen, setLanguageOpen] = useState(false);
    const languageRef = useRef<HTMLDivElement>(null);
    const location = useLocation();
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (languageRef.current && !languageRef.current.contains(event.target as Node)) {
                setLanguageOpen(false);
            }
        };
        document.addEventListener("mousedown", handleClickOutside);
        return () => { document.removeEventListener("mousedown", handleClickOutside); };
    }, []);

    useEffect(() => {
        document.body.style.overflow = mobileMenuOpen ? "hidden" : "";

        return () => {
            document.body.style.overflow = "";
        };
    }, [mobileMenuOpen]);

    const LARGE_HEADER_ROUTES = [
        "/welcome",
        "/inactive",
        "/login",
        "/logout",
    ];

    const isLargeHeader = LARGE_HEADER_ROUTES.includes(location.pathname);
    const isLogin = location.pathname === "/login";
    const isDarkHeader = location.pathname.includes("/settings")

    const headerHeight = isLargeHeader ? "md:h-[120px] h-[75px]" : "h-[75px]";
    const navLinks = !isLargeHeader;
    const showMobileMenu = true;
    const topPosition = isLargeHeader ? "fixed top-0" : "sticky top-0";
    const languageSelect = isLargeHeader ? "top-[115px]" : "top-[70px]";
    const logoSize = isLargeHeader ? "text-3xl md:text-6xl" : "text-3xl";

    const headerBackground = isLogin
        ? "bg-[url('src/assets/img/winter_forrest.webp')] bg-cover bg-center"
        : isDarkHeader
            ? "bg-dark-navy"
            : "bg-nordiska-blue";

    const hamburgerBackground = isLogin
        ? "bg-login-bg"
        : isDarkHeader
            ? "bg-dark-navy"
            : "bg-nordiska-blue";
    
    const languageSelectBg = isLogin ? "" : isDarkHeader
            ? "bg-dark-navy"
            : "bg-nordiska-blue";
    const dotColor = isLogin ? "text-white" : "text-nordiska-orange";

    const animateHeader = ["/welcome", "/inactive", "/login", "/logout",].includes(location.pathname);
    // const animateHeader = ["/accounts", "/transactions", "/transfer",].includes(location.pathname);

    return (
        <>
        {/* ----- DESKTOP ----- */}
        {/* ------------------- */}
        <motion.header 
            key={location.pathname}
            initial={animateHeader ? { y: "-100%" } : false}
            animate={{ y: 0 }}
            transition={{
                duration: 0.5,
                ease: "easeOut",
            }}
            className={`${topPosition} z-[1001] w-screen ${headerHeight} flex items-end justify-between ${headerBackground} gap-10 pt-0 px-5 pb-4`}>

            {/* ----- LOGO ----- */}
            <a
                href="/"
                className={`font-montserrat-alternates ${logoSize} text-white font-bold tracking-wider whitespace-nowrap cursor-pointer}`}
            >
                nordiska<span className={dotColor}>.</span>
            </a>

            {/* ----- LÄNKAR ----- */}
                <div className="hidden items-center gap-15 md:flex">
                    {navLinks && (
                    <nav 
                        aria-label={t("aria-label.page-nav")}
                        className="flex gap-10 text-[14px] uppercase tracking-[0.18em] text-white"
                    >
                        <PageLink route="/" title={t("page-header.my-nordiska")} header/>
                        <PageLink route="/help" title={t("page-header.help-center")} header/>
                        <PageLink route="/settings" title={t("page-header.settings")} header/>
                        <LogoutButton title={t("page-header.logout")} />
                    </nav>
                    )}

                    {/* ----- SPRÅK ----- */}
                    <div ref={languageRef} className="relative">
                        <button
                            type="button"
                            onClick={() => setLanguageOpen(!languageOpen)}
                            aria-expanded={languageOpen}
                            aria-haspopup="true"
                            className="flex cursor-pointer items-center gap-2 text-[14px] font-light uppercase tracking-[0.18em] text-white"
                        >
                            <img
                                className="h-[14px] w-[19px] invert -mr-1.5"
                                src="icons/lang-icon.svg"
                                alt=""
                                aria-hidden="true"
                            />
                            {i18n.language}
                        </button>

                        {languageOpen && (
                            <div
                                className={`fixed ${languageSelect} right-0 flex h-12 w-25 items-center justify-center rounded-bl-2xl ${languageSelectBg} `}
                            >
                                <button
                                    type="button"
                                    onClick={() => {
                                        i18n.changeLanguage(
                                            i18n.language === "sv"
                                                ? "en"
                                                : "sv"
                                        );
                                        setLanguageOpen(false);
                                    }}
                                    className="cursor-pointer ml-2 px-3 py-2 text-[14px] uppercase tracking-[0.18em] text-white"
                                >
                                    {i18n.language === "sv"
                                        ? "English"
                                        : "Svenska"}
                                </button>
                            </div>
                        )}
                    </div>
                </div>

            {/* ----- MOBILE ----- */}
            {/* ------------------ */}

            {/* ----- HAMBURGER ----- */}
            {showMobileMenu && (
                <button
                    type="button"
                    onClick={() =>
                        setMobileMenuOpen(!mobileMenuOpen)
                    }
                    aria-label={
                        mobileMenuOpen
                            ? t("aria-label.mobile-menu-close")
                            : t("aria-label.mobile-menu-open")
                    }
                    aria-expanded={mobileMenuOpen}
                    className="flex h-11 w-11 cursor-pointer items-center justify-center text-white md:hidden"
                >
                    <span
                        aria-hidden="true"
                        className="text-4xl font-light leading-none"
                    >
                        {mobileMenuOpen ? "×" : "☰"}
                    </span>
                </button>
            )}
        </motion.header>

        {/* ----- MOBILE MENY ----- */}
        <AnimatePresence>
            {mobileMenuOpen && (
                <motion.div
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    exit={{ opacity: 0 }}
                    transition={{ duration: 0.2 }}
                    className={`fixed inset-0 z-[1000] flex flex-col ${hamburgerBackground} px-6 pb-10 pt-[110px] md:hidden justify-end`}
                >
                    
                    {/* ----- LÄNKAR ----- */}
                {navLinks && (
                    <nav
                        aria-label={t("aria-label.mobile-nav")}
                        className="flex flex-1 flex-col items-center justify-center gap-8 font-montserrat text-white text-xl font-semibold uppercase tracking-wider"
                    >
                        <Link
                            to="/"
                            onClick={() => setMobileMenuOpen(false)}
                        >
                            {t("page-header.my-nordiska")}
                        </Link>
                        <Link
                            to="/help"
                            onClick={() => setMobileMenuOpen(false)}
                        >
                            {t("page-header.help-center")}
                        </Link>
                        <Link
                            to="/settings"
                            onClick={() => setMobileMenuOpen(false)}
                        >
                            {t("page-header.settings")}
                        </Link>

                        <div onClick={() => setMobileMenuOpen(false)}>
                            <LogoutButton title={t("page-header.logout")} mobile={true} />
                        </div>
                    </nav>
                )}

                    {/* ----- SPRÅK ----- */}
                    <div
                        onClick={() => setMobileMenuOpen(false)}
                        className="flex justify-center">
                        <button
                            type="button"
                            onClick={() =>
                                i18n.changeLanguage(
                                    i18n.language === "sv" ? "en" : "sv"
                                )
                            }
                            className="flex min-h-11 cursor-pointer items-center gap-2 px-4 text-sm font-light uppercase tracking-[0.18em] text-white"
                        >
                            <img
                                className="h-[14px] w-[19px] invert"
                                src="icons/lang-icon.svg"
                                alt=""
                                aria-hidden="true"
                            />
                            {i18n.language === "sv" ? "English" : "Svenska"}
                        </button>
                    </div>
                </motion.div>
            )}
        </AnimatePresence>
        </>
    )
}
