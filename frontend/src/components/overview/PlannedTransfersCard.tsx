import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { toTimestamp } from "../../utils/date";
import { useTransactions } from "../../hooks/useTransactions";
import { LoadingState, ErrorState } from "../StatusMessage";
import { useNavigate } from "react-router";
import { formatCurrencySmallSek } from "../../utils/currency";
import { formatDate } from "../../utils/date";
import { useGetAccounts } from "../../hooks/useAccounts";
import OverviewCardHeader from "./OverviewCardHeader";
import { OverviewCardRow } from "./OverviewCardRow";
import type { OverviewCardProps } from "../../pages/OverviewPage";

export function NoPlannedState() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-light-blue-accent mask-[url('/icons/transfers.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold mt-1 text-dark-navy/80"> {t("no-planned-state.title")} </p>
            <p className="px-4 text-center text-[0.8em] mt-1 text-dark-navy/80"> {t("no-planned-state.description")}</p>
            <p className="mt-4 text-center text-sm font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/transfer")} className="text-primary-blue cursor-pointer hover:text-primary-hover">{t("page-navigation.transfers")}</span></p>
        </div>
    );
}

export default function PlannedTransfersCard({ showOnlyFavs }: OverviewCardProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: transactions, isPending, isError } = useTransactions({ Page: 1, PageSize: 50, AccountIds: [], IsPlanned: true });
    const { data: accounts } = useGetAccounts();

    const startOfToday = new Date().setHours(0, 0, 0, 0);

    const upcoming = (transactions?.items ?? [])
        .filter((tx) => toTimestamp(tx.plannedDate) >= startOfToday)
        .filter((tx) => {
            if (!showOnlyFavs) return true;
            return accounts?.some((account) => (account.id === tx.accountId || account.id === tx.targetAccountId) && account.isFavorite);
        })
        .sort((a, b) => toTimestamp(a.plannedDate) - toTimestamp(b.plannedDate))

    const getAccountNumber = (accountId: number | string | undefined ) => {
        const account = accounts?.find((account) => account.id === accountId);
        if (account?.status === "closed"){ return t("overview-route.transactions-card.closed-account")}
        return account?.accountNumber ?? "";
    };

    return (
    <OverviewCard>
        <div className="flex h-full flex-col">
            
            {/* CARD HEADER */}
            <OverviewCardHeader title={t("overview-route.planned-card.title")} description={t("overview-route.planned-card.description")} />

            {/* USER FEEDBACK */}
            {isPending && ( <LoadingState message={t("overview-route.planned-card.pending")} />)}
            {isError && ( <ErrorState message={t("overview-route.planned-card.error")} />)}
            {upcoming.length === 0 && (<NoPlannedState/>)}

            {/* CONTENT ROWS */}
            {!isPending && !isError && (
                <section className="flex min-h-0 flex-1 flex-col ">
                    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
                        <div className="min-h-0 flex-1 overflow-y-auto">

                            {upcoming.map((tx) => {

                                const displayName = tx.label ? tx.label : t(`transactions-route.type-transfer`);
                                const displayDate = <>{formatDate(tx.plannedDate)} {tx.repeating ? <span className="font-medium capitalize"> {t(`page-transfer.repeating.${tx.repeating}`)}</span> : "" }</>
                                const displayFromTo = <>{getAccountNumber(tx.accountId)} → {getAccountNumber(tx.targetAccountId)}</>

                                return (
                                    <OverviewCardRow
                                        key={tx.id}
                                        btnHoverText={t("overview-route.planned-card.btn-hover")}
                                        onClick={() => {navigate("/transfer")}}
                                        cols={"2"}
                                        titleHoverText={displayName}
                                        titleRow={[displayName, formatCurrencySmallSek(tx.amount)]}
                                        descriptionRow={[displayDate, displayFromTo]}
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