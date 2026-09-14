import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";

export default function PageNavigation() {
    const {t} = useTranslation();
    const navigate = useNavigate(); 

    return (
        <>
        {/* ----- DESKTOP ----- */}
        <nav className="hidden md:flex w-screen bg-white justify-start pl-5 gap-10 h-[47px] pb-[4px] border-b-2 border-light-gray sticky top-[75px]">
            <PageLink title={t("page-navigation.overview")} route="/" />
            <PageLink title={t("page-navigation.accounts")} route="/accounts" />
            <PageLink title={t("page-navigation.transactions")} route="/transactions" />
            <PageLink title={t("page-navigation.transfers")} route="/transfer" />
        </nav>

        {/* ----- MOBIL ----- */}
        <nav className="fixed bottom-0 left-0 z-50 flex h-[70px] w-full bg-nordiska-blue md:hidden">
            <div
                onClick={() => navigate("/accounts")}
                className="relative flex flex-1 flex-col items-center justify-center after:absolute after:right-0 after:top-[10%] after:h-[80%] after:w-px after:bg-white">
                <img src="/public/icons/accounts.svg" alt="" className="h-6 w-6 invert" />
                <p className="mt-1 text-[8pt] text-white"> {t("page-navigation.accounts")} </p>
            </div>

            <div
                onClick={() => navigate("/transactions")} 
                className="relative flex flex-1 flex-col items-center justify-center after:absolute after:right-0 after:top-[10%] after:h-[80%] after:w-px after:bg-white">
                <img src="/public/icons/transactions.svg" alt="" className="h-6 w-6 invert" />
                <p className="mt-1 text-[8pt] text-white"> {t("page-navigation.transactions")} </p>
            </div>

            <div
                onClick={() => navigate("/overview")}
                className="relative flex flex-1 flex-col items-center justify-center after:absolute after:right-0 after:top-[10%] after:h-[80%] after:w-px after:bg-white">
                <img src="/public/icons/overview.svg" alt="" className="h-6 w-6 invert" />
                <p className="mt-1 text-[8pt] text-white"> {t("page-navigation.overview")} </p>
            </div>

            <div
                onClick={() => navigate("/transfer")}
                className="flex flex-1 flex-col items-center justify-center">
                <img src="/public/icons/transfers.svg" alt="" className="h-6 w-6 invert" />
                <p className="mt-1 text-[8pt] text-white"> {t("page-navigation.transfers")} </p>
            </div>
        </nav>
        </>
    )
}