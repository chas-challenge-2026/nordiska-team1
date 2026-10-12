import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";

export default function PageNavigation() {
    const { t } = useTranslation();

    let bgGradient = 100;
    const elements = [];
    
    for (let i = 76; i != 140; i++) {
        elements.push(
            <div 
                key={i} 
                className= "z-950 hidden md:block fixed top-0 left-0 w-full blur-sm"
                style={{
                    height: `${i}px`,
                  
                    backgroundColor: "#F7F7F6",
                    opacity: `${bgGradient / 100 / 2}`,
                }}
            />
        );
        bgGradient -= 1.5;
    }

    return (
        <>
        {/* DESKTOP */}
        <div className="hidden md:block">
            {elements} {/* Gradient */}
            <nav className="w-fit mx-auto mt-3 fixed left-1/2 -translate-x-1/2 z-990 top-[75px]">
                <div className="bg-dark-navy py-3 px-10 flex gap-8 rounded-4xl shadow-sm">
                    <PageLink title={t("page-navigation.overview")} route="/" />
                    <PageLink title={t("page-navigation.accounts")} route="/accounts" />
                    <PageLink title={t("page-navigation.transactions")} route="/transactions" />
                    <PageLink title={t("page-navigation.transfers")} route="/transfer" />
                </div>
            </nav>
        </div>

        {/* MOBIL */}
        <div className="block md:hidden ">
            <div className="fixed -bottom-1 left-0 right-0 h-[50px] bg-nordiska-bg z-40">
                <nav aria-label={t("aria-lable.mobile-nav")} className="fixed bottom-2 left-1/2 -translate-x-1/2 z-50 w-[98vw] h-[55px] bg-dark-navy rounded-4xl">
                    <div className="relative w-full h-full grid grid-flow-col auto-cols-fr items-center justify-items-center px-2">
                        <PageLink title={t("page-navigation.transactions")} route="/transactions" mobile="mask-[url('/icons/transactions.svg')]" />
                        <PageLink title={t("page-navigation.accounts")} route="/accounts" mobile="mask-[url('/icons/accounts.svg')]"/>
                        <PageLink title={t("page-navigation.transfers")} route="/transfer" mobile="mask-[url('/icons/transfers.svg')]" />
                        <PageLink title={t("page-navigation.overview")} route="/" mobile="mask-[url('/icons/overview.svg')]" />
                    </div>
                </nav>
            </div>
        </div>
        </>
    )
}