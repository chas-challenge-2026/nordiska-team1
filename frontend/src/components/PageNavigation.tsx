import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";
import { NavLink } from "react-router";

export default function PageNavigation() {
    const { t } = useTranslation();

    const mobileItems = [
        { to: "/accounts", icon: "/icons/accounts.svg", label: t("page-navigation.accounts") },
        { to: "/transactions", icon: "/icons/transactions.svg", label: t("page-navigation.transactions") },
        { to: "/", icon: "/icons/overview.svg", label: t("page-navigation.overview") },
        { to: "/transfer", icon: "/icons/transfers.svg", label: t("page-navigation.transfers") },
    ];
    const divider = "after:absolute after:right-0 after:top-[10%] after:h-[80%] after:w-px after:bg-white";

    return (
        <>
            {/* ----- DESKTOP ----- */}
            <nav className="hidden md:flex w-screen bg-white justify-start pl-5 gap-10 h-[47px] pb-[4px] border-b-2 border-light-gray sticky top-[75px] z-40">
                <PageLink title={t("page-navigation.overview")} route="/" />
                <PageLink title={t("page-navigation.accounts")} route="/accounts" />
                <PageLink title={t("page-navigation.transactions")} route="/transactions" />
                <PageLink title={t("page-navigation.transfers")} route="/transfer" />
            </nav>

            {/* ----- MOBIL ----- */}
            <nav aria-label={t("aria-lable.mobile-nav")} className="fixed bottom-0 left-0 z-50 h-[60px] w-full bg-nordiska-blue md:hidden">
                <ul className="flex h-full">
                    {mobileItems.map((item, i) => (
                        <li key={item.to} className={`relative flex-1 ${i < mobileItems.length - 1 ? divider : ""}`}>
                            <NavLink
                                to={item.to}
                                end={item.to === "/"}
                                className={({ isActive }) =>
                                    `flex h-full flex-col items-center justify-center ${isActive ? "bg-white/10" : ""}`
                                }
                            >
                                <img src={item.icon} alt="" className="h-6 w-6 invert" />
                                <span className="mt-1 text-[8pt] text-white">{item.label}</span>
                            </NavLink>
                        </li>
                    ))}
                </ul>
            </nav>
        </>
    )
}
