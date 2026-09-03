import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";

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

    return (
        <header className={`${fixedPos ? "fixed top-0 z-1001" : ""} flex w-screen h-[112px] items-center ${login ? "bg-[url('src/assets/img/winter_forrest.webp')] bg-cover bg-center" : "bg-nordiska-blue"} gap-10`}>

            {/* LOGO */}
            <div className="items-center ml-3 mr-auto min-w-0">
                <a
                    href="/"
                    className="font-montserrat-alternates text-6xl text-white font-bold tracking-wider whitespace-nowrap"
                >
                    nordiska<span className={login ? "text-white": "text-nordiska-orange"}>.</span>
                </a>
            </div>

            {/* NAVIGATION */}
            {navLinks && (
            <nav className="flex items-center justify-center h-full gap-10">
                <PageLink title={t("header-navigation.customer")} route="/help" />
                <PageLink title={t("header-navigation.settings")} route="/settings" />
                <PageLink title={t("header-navigation.logout")} route="/logout" />
            </nav>
            )}

            <div className="flex mr-3 gap-2">
                <img className="w-[24px] invert" src="icons/lang-icon.svg" alt="" />
                <select
                    name="lang"
                    id="lang"
                    value={i18n.language}
                    onChange={(e) => i18n.changeLanguage(e.target.value)}
                    className="text-white"
                >
                    <option value="sv">SV</option>
                    <option value="en">EN</option>
                </select>
            </div>
        </header>
    )
}