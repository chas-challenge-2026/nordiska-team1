import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";

export default function MobileNavigation() {
    const { t } = useTranslation();

    return (
        // MOBIL 
        <div className="block lg:hidden ">
            <div className="fixed -bottom-1 left-0 right-0 h-[50px] bg-nordiska-bg z-40">
                <nav aria-label={t("aria-lable.mobile-nav")} className="fixed bottom-2 left-1/2 -translate-x-1/2 z-50 w-[98vw] h-[50px] bg-dark-navy rounded-4xl">
                    <div className="relative w-full h-full grid grid-flow-col auto-cols-fr items-center justify-items-center px-2">
                        <PageLink title={t("page-navigation.transactions")} route="/transactions" mobile="mask-[url('/icons/transactions.svg')]" />
                        <PageLink title={t("page-navigation.accounts")} route="/accounts" mobile="mask-[url('/icons/accounts.svg')]"/>
                        <PageLink title={t("page-navigation.transfers")} route="/transfer" mobile="mask-[url('/icons/transfers.svg')]" />
                        <PageLink title={t("page-navigation.overview")} route="/" mobile="mask-[url('/icons/overview.svg')]" />
                    </div>
                </nav>
            </div>
        </div>
    )
}