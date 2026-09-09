import PageLink from "./PageLink";
import { useTranslation } from "react-i18next";

export default function PageNavigation() {
    const {t} = useTranslation();

    return (
        <nav className="flex items-center justify-center gap-10 pt-10 pb-3">
            <PageLink title={t("page-navigation.overview")} route="/" />
            <PageLink title={t("page-navigation.accounts")} route="/accounts" />
            <PageLink title={t("page-navigation.transactions")} route="/transactions" />
            <PageLink title={t("page-navigation.transfers")} route="/transfer" />
        </nav>
    )
}