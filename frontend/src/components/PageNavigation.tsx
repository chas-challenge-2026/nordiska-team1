import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";

export default function PageNavigation() {
    const {t} = useTranslation();

    return (
        <nav className="flex w-screen bg-white justify-start pl-10 gap-10 h-[60px] border-b-2 border-light-gray sticky top-[100px]">
            <PageLink title={t("page-navigation.overview")} route="/" />
            <PageLink title={t("page-navigation.accounts")} route="/accounts" />
            <PageLink title={t("page-navigation.transactions")} route="/transactions" />
            <PageLink title={t("page-navigation.transfers")} route="/transfer" />
        </nav>
    )
}