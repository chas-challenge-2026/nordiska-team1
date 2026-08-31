import PageLink from "./PageLink"

export default function PageHeader() {
    return (
        <header className="fixed top-0 w-screen h-[112px] flex items-center bg-nordiska-blue border-b border-white z-[1001]">

            {/* LOGO */}
            <div className="flex-1 flex items-center ml-3">
                <a
                    href="/"
                    className="font-montserrat-alternates text-6xl text-white font-bold tracking-wider whitespace-nowrap"
                >
                    nordiska<span className="text-nordiska-orange">.</span>
                </a>
            </div>

            {/* NAVIGATION */}
            <nav className="flex-1 flex items-center justify-center gap-10 h-full">
                <PageLink title="Hjälpcenter" route="/help" />
                <PageLink title="Inställningar" route="/settings" />
                <PageLink title="Logga ut" route="/logout" />
                <PageLink title="Språkval" route="/language" />
            </nav>

        </header>
    )
}