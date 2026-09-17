import PageLink from "../PageLink";
import { Link } from "react-router";
import LogoutButton from "./LogoutButton";
import LanguageButton from "./LanguageBtn";
import { useTranslation } from "react-i18next";
import { useEffect, useRef, useState } from "react";
import {motion, AnimatePresence} from "motion/react";
import { useUserStore } from "../../store/userStore";

type pageHeaderProps = {
    protectedHeader : boolean
}

export default function PageHeader({protectedHeader}: pageHeaderProps) {
    // ----- USER UI -----
    const user = useUserStore((state) => state.user);
    const username = user?.name?.split(" ")[0] || user?.email?.split("@")[0] || "user";

    // ----- LANGUAGE ----- 
    const { t, i18n } = useTranslation();
    const [languageOpen, setLanguageOpen] = useState(false);
    const languageRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (languageRef.current && !languageRef.current.contains(event.target as Node)) {
                setLanguageOpen(false);
            }
        };
        document.addEventListener("mousedown", handleClickOutside);
        return () => { document.removeEventListener("mousedown", handleClickOutside); };
    }, []);

    // ----- NAV: add more navLinks below -----
    const menuItems = [
        { route: "/", title: t("page-header.my-nordiska"),},
        { route: "/help", title: t("page-header.help-center"),},
        { route: "/settings", title: t("page-header.settings"),},
    ];

    // ----- STYLING -----
    const [menuOpen, setMenuOpen] = useState(false);
    const closeMenu = () => setMenuOpen(false);
    let headerBackground = "bg-nordiska-blue"
    let dotColor = "text-nordiska-orange"
    let languageSelectBg = "bg-nordiska-blue"

    switch (location.pathname) {
        case "/login":
            dotColor = "text-white"
            headerBackground = "bg-[url('/images/winter_forrest.webp')] bg-cover bg-center"
            languageSelectBg = ""
            break;
        
        case "/settings":
        case "/help":
            headerBackground = "bg-dark-navy"
    }

    return (
    <>
        {!protectedHeader 
        ? ( // ----- NOT PROTECTED (LARGE) HEADER -----
            <motion.header
                key={location.pathname}
                initial= {{y: "-100%"}}
                animate= {{y: 0}}
                transition={{
                    duration: 0.5,
                    ease: "easeOut",
                }}
                className={`fixed top-0 z-[1001] w-screen md:h-[120px] h-[75px] flex items-end justify-between ${headerBackground} pt-0 px-5 pb-2 md:pb-3`}
                > 

                    {/* ----- LOGO ----- */}
                    <a href="/welcome" className={`font-montserrat-alternates text-3xl md:text-6xl text-white font-bold tracking-wider whitespace-nowrap cursor-pointer} -mb-1`}>
                        nordiska<span className={dotColor}>.</span>
                    </a>

                {/* ----- SPRÅK ----- */}
                    <div ref={languageRef} className="relative">
                        <button
                            type="button"
                            onClick={() => setLanguageOpen(!languageOpen)}
                            aria-expanded={languageOpen}
                            aria-haspopup="true"
                            className="flex cursor-pointer items-center gap-2 text-[14px] font-light uppercase tracking-[0.18em] text-white font-normal"
                        >
                            <img className="h-[14px] w-[19px] invert -mr-1.5" src="icons/lang-icon.svg" aria-hidden="true"/>
                            {i18n.language}
                        </button>

                        {languageOpen && (
                            <div className={`fixed  top-[70px] md:top-[115px] right-0 flex h-10 w-25 items-center justify-center rounded-bl-2xl ${languageSelectBg} `}>
                                <button
                                    type="button"
                                    onClick={() => {
                                        i18n.changeLanguage(i18n.language === "sv" ? "en" : "sv");
                                        setLanguageOpen(false);
                                    }}
                                    className="cursor-pointer ml-2 px-3 py-2 text-[14px] uppercase tracking-[0.18em] text-white"
                                >
                                    {i18n.language === "sv" ? "English" : "Svenska"}
                                </button>
                            </div>
                        )}
                    </div>
                
            </motion.header>

        ) : ( // ----- PROTECTED HEADER -----
        <>
        <header className={`sticky top-0 z-[1001] w-screen h-[60px] md:h-[75px] flex items-end justify-between ${headerBackground} pt-0 px-5 pb-2 md:pb-3`}>

            {/* ----- LOGO ----- */}
            <section>
                <a href="/" title={t("page-header.home")} className={`font-montserrat-alternates text-3xl text-white font-bold tracking-wider whitespace-nowrap cursor-pointer}`}>
                    nordiska<span className={dotColor}>.</span>
                </a>
            </section>

            {/* ----- HELP & MENU ----- */}
            <section className="flex -mr-1 mb-0.5">
                <Link
                    title = {t("page-header.help-center")}
                    to="/help"
                    onClick={closeMenu}
                    aria-label={t("page-header.help-center")} 
                    className="flex min-h-11 min-w-11 items-end justify-end text-white"
                >
                    <img src="/icons/help.svg" alt="" aria-hidden="true" className="h-7 w-7 invert" />
                </Link>

                <button
                    title={t("page-header.menu")}
                    type="button"
                    onClick={() => setMenuOpen(!menuOpen) }
                    aria-label={
                        menuOpen
                            ? t("aria-label.mobile-menu-close")
                            : t("aria-label.mobile-menu-open")
                    }
                    aria-expanded={menuOpen}
                    className="flex h-11 w-11 cursor-pointer items-end justify-end text-white"
                >
                    <span
                        aria-hidden="true"
                        className="text-4xl font-light leading-none"
                    >
                        {menuOpen 
                            ? <img src="/icons/close.svg" alt="" aria-hidden="true" className="h-7 w-7 invert" /> 
                            : <img src="/icons/hamburger.svg" alt="" aria-hidden="true" className="h-7 w-7 invert" />}
                    </span>
                </button>
            </section>
        </header>

        <AnimatePresence>
        {menuOpen && (
        <>
            {/* OVERLAY */}
            <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 0.8 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.3 }}
                onClick={closeMenu}
                className={`fixed inset-0 z-[999] hidden md:block ${headerBackground}`}
                aria-hidden="true"
            />

            {/* MENY */}
            <motion.aside
                initial={{ x: "100%", opacity: 0,}}
                animate={{ x: 0, opacity: 1,}}
                exit={{ x: "100%", opacity: 0,}}
                transition={{
                    duration: 0.4,
                    ease: [0.4, 0, 0.2, 1],
                }}
                    className={`fixed right-0 top-0 z-[1000] flex h-screen flex-col inset-0 justify-end ${headerBackground} px-6 pb-10 pt-[110px] text-white shadow-2xl md:left-auto md:right-0 md:w-[350px]`}
            >
                <div className="flex flex-1 flex-col items-center">

                    {/* NAVLINKS */}
                    <nav className="flex flex-1 flex-col items-center justify-center gap-8 font-montserrat text-xl font-semibold uppercase tracking-wider">
                        {menuItems.map((item) => (
                            <div key={item.route} onClick={closeMenu}>
                                <PageLink route={item.route} title={item.title} header/>
                            </div>
                        ))}
                    </nav>

                    {/* USER & LOG OUT */}
                    <div className="flex flex-col items-center font-normal mb-30">
                        <p className="mb-5 text-xs capitalize">
                            {t("page-header.user")}
                            <span className="text-sm font-medium"> {username} </span>
                        </p>

                        <div onClick={closeMenu} className="text-xl font-medium">
                            <LogoutButton title={t("page-header.logout")}/>
                        </div>
                    </div>

                    {/* SPRÅK */}
                    <div className="flex justify-center">
                        <LanguageButton />
                    </div>

                </div>
            </motion.aside>
        </>
    )}
</AnimatePresence>
        </>
        )}
    </>
    );
}