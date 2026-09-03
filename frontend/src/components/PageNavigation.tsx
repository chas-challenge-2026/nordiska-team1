import PageLink from "./PageLink";

export default function PageNavigation() {
    return (
        <nav className="flex items-center justify-center gap-10 pt-10">
            <PageLink title="Översikt" route="/" />
            <PageLink title="Bankkonton" route="/accounts" />
            <PageLink title="Transaktioner" route="/transactions" />
            <PageLink title="Överföringar" route="/transfer" />
        </nav>
    )
}
