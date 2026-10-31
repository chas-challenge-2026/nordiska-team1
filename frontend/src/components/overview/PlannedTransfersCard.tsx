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
import { NoPlannedState } from "../StatusMessage";

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