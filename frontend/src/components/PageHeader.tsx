import PageLink from "./PageLink"

type PageHeaderProps = {
    navLinks?: boolean;
    login?: boolean;
}

/**
 * Header för applikationen.
 * navLinks styr om navigationen visas.
 * login ändrar headerns styling för login-sidan.
 */

export default function PageHeader({navLinks = true, login = false}: PageHeaderProps) {
    return (
        <header className={`flex h-[112px] items-center ${login ? "bg-[url('src/assets/img/winter_forrest.webp')] bg-cover bg-center" : "bg-nordiska-blue"} border-b border-white gap-10`}>

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
                <PageLink title="Hjälpcenter" route="/help" />
                <PageLink title="Inställningar" route="/settings" />
                <PageLink title="Logga ut" route="/logout" />
            </nav>
            )}

            <div className="flex mr-3 gap-2">
                <img className="w-[24px] invert" src="icons/lang-icon.svg" alt="" />
                <select name="lang" id="lang" className="text-white">
                    <option value="SV">SV</option>
                    <option value="EN">EN</option>
                </select>
            </div>
        </header>
    )
}