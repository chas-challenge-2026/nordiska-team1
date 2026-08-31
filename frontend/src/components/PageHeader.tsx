import PageLink from "./PageLink"

export default function PageHeader() {
    return (
        <header className="flex h-[112px] items-center bg-nordiska-blue border-b border-white gap-10">

            {/* LOGO */}
            <div className="items-center ml-3 mr-auto min-w-0">
                <a
                    href="/"
                    className="font-montserrat-alternates text-6xl text-white font-bold tracking-wider whitespace-nowrap"
                >
                    nordiska<span className="text-nordiska-orange">.</span>
                </a>
            </div>

            {/* NAVIGATION */}
            <nav className="flex items-center justify-center h-full gap-10">
                <PageLink title="Hjälpcenter" route="/help" />
                <PageLink title="Inställningar" route="/settings" />
                <PageLink title="Logga ut" route="/logout" />
            </nav>
            <button className="flex mr-3 gap-2 text-white cursor-pointer"><img className="w-[24px] invert" src="icons/lang-icon.svg" alt="" />SV</button>

        </header>
    )
}