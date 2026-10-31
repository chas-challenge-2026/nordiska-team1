import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { useGetAccounts } from "../../hooks/useAccounts";
import { LoadingState, ErrorState } from "../StatusMessage";
import { useNavigate } from "react-router";
import { formatCurrencySmallSek } from "../../utils/currency";
import OverviewCardHeader from "./OverviewCardHeader";
import { OverviewCardRow } from "./OverviewCardRow";
import type { OverviewCardProps } from "../../pages/OverviewPage";

function NoFavorites() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-light-blue-accent mask-[url('/icons/star.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold mt-1 text-dark-navy/80">{t("overview-route.no-favs")}</p>
            <p className="px-4 text-center text-[0.8em] mt-1 text-dark-navy/80">{t("overview-route.no-favs-description")}</p>
            <p className="mt-4 text-center text-sm font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/transfer")} className="text-primary-blue cursor-pointer hover:text-primary-hover">{t("page-navigation.accounts")}</span></p>
        </div>
    );
}

export default function BalanceCard({ showOnlyFavs }: OverviewCardProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: accounts, isPending, isError } = useGetAccounts("active");

    const displayedAccounts = (accounts ?? []).filter((account) => !showOnlyFavs || account.isFavorite);

    return (
    <OverviewCard>
        <div className="flex h-full flex-col">

            {/* CARD HEADER */}
            <OverviewCardHeader title={t("overview-route.account-card.title")} description={t("overview-route.account-card.description")} />

            {/* USER FEEDBACK */}
            {displayedAccounts.length === 0 && showOnlyFavs && (<NoFavorites />)}
            {isPending && ( <LoadingState title={t("overview-route.account-card.pending")} />)}
            {isError && ( <ErrorState title={t("overview-route.account-card.error")} />)}

            {/* CONTENT */}
            {!isPending && !isError && (
                <section className="flex min-h-0 flex-1 flex-col ">
                    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">

                        {/* KORT RADER */}
                        <div className="min-h-0 flex-1 overflow-y-auto">
                            {displayedAccounts?.map((account) => {

                                const displayName = account.accountName ? account.accountName : t(`accounts-route.account-type-${account.accountType}`);

                                return (
                                    <OverviewCardRow 
                                        key={account.id}
                                        btnHoverText={`${t("overview-route.account-card.hover-title")} ${account.accountNumber}`}
                                        onClick={() => navigate(`/accounts?account=${account.id}`)}
                                        cols={"2"}
                                        titleHoverText={displayName}
                                        titleRow={[displayName, formatCurrencySmallSek(account.balance)]}
                                        accountFavorite={account.isFavorite}
                                        descriptionRow={[account.accountNumber, t(`accounts-route.account-type-${account.accountType}`)]}
                                    />
                                )
                            })}
                        </div>
                    </div>
                </section>
            )}
        </div>
    </OverviewCard>
    );
}