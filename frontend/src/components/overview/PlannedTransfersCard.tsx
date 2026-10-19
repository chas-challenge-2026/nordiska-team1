import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { toTimestamp } from "../../utils/date";
import { useTransactions } from "../../hooks/useTransactions";
import { LoadingState, ErrorState } from "../StatusMessage";
import { useNavigate } from "react-router";
import { formatCurrency } from "../../utils/currency";
import { formatDate } from "../../utils/date";
import { useGetAccounts } from "../../hooks/useAccounts";

const MAX_ROWS = 4;

export function NoPlannedState() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-16 w-16 bg-nordiska-orange mask-[url('/icons/transfers.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="xl:text-lg font-semibold mt-1 text-dark-navy/80"> {t("no-planned-state.title")} </p>
            <p className="px-10 text-center text-sm mt-1 text-dark-navy/80"> {t("no-planned-state.description")}</p>
            <p className="mt-4 text-[0.9em] xl:text-base font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/transfer")} className="text-primary-blue cursor-pointer hover:text-nordiska-blue">{t("page-navigation.transfers")}</span></p>
        </div>
    );
}

export default function PlannedTransfersCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: transactions, isPending, isError } = useTransactions({ Page: 1, PageSize: 100, AccountIds: [], IsPlanned: true });
    const { data: accounts } = useGetAccounts();

    const startOfToday = new Date().setHours(0, 0, 0, 0);

    const upcoming = (transactions?.items ?? [])
        .filter((tx) => toTimestamp(tx.plannedDate) >= startOfToday)
        .sort((a, b) => toTimestamp(a.plannedDate) - toTimestamp(b.plannedDate))
        .slice(0, MAX_ROWS);

    const getAccountNumber = (accountId: number | string | undefined ) => {
        const account = accounts?.find((account) => account.id === accountId);
        if (account?.status === "closed"){ return t("overview-route.transactions-card.closed-account")}
        return account?.accountNumber ?? "";
    };

    return (
    <OverviewCard>
        <div className="flex h-full flex-col">
            
            {/* CARD HEADER */}
            <div className="h-22 bg-dark-navy pl-2 pt-1.5 text-white sm:pl-3">
                <div className="flex flex-col items-start p-3 sm:p-4">
                    <h2 className="text-xl font-semibold tracking-wide sm:text-2xl"> {t("overview-route.planned-card.title")} </h2>
                    <p className="text-[0.65em] uppercase text-white/80 sm:text-[0.75em]"> {t("overview-route.planned-card.description")}  </p>
               </div>
            </div>

            {/* USER FEEDBACK */}
            {isPending && ( <LoadingState message={t("overview-route.planned-card.pending")} />)}
            {isError && ( <ErrorState message={t("overview-route.planned-card.error")} />)}
            {upcoming.length === 0 && (<NoPlannedState/>)}

            <section className="flex flex-1 flex-col p-2 sm:p-3">
            <div className="flex flex-1 flex-col overflow-hidden">
                {/* TRANSFER ROWS */}
                {upcoming.slice(0, MAX_ROWS).map((tx) => (
                    <button 
                        key={tx.id}
                        type="button"
                        onClick={() => {navigate("/transfer")}}
                        className={`flex basis-1/5 shrink-0 flex-col justify-center px-3 py-2  border-nordiska-orange odd:bg-light-gray/60 even:bg-white sm:px-5 cursor-pointer hover:bg-primary-blue/10 `}
                    >
                        {/* NAMN & SUMMA */}
                        <p className="flex items-baseline justify-between gap-2 text-sm sm:text-base">
                            <span className="min-w-0 wrap-break-word font-semibold truncate"> {tx.label ? tx.label : t(`transactions-route.type-transfer`)} </span>
                            <span className={`shrink-0 whitespace-nowrap font-medium`}>{formatCurrency(tx.amount)}</span>
                        </p>

                        {/* DATUM & INTERVALL / FRÅN TILL */}
                        <p className="flex flex-wrap justify-between gap-x-2 gap-y-1 text-[9px] uppercase text-dark-navy/60 sm:text-xs">
                            <span className="min-w-0 wrap-break-word">{formatDate(tx.plannedDate)}: <span className="font-medium capitalize">{t(`page-transfer.repeating.${tx.repeating}`)}</span></span> 
                            <span className="shrink-0 whitespace-nowrap">
                                {getAccountNumber(tx.accountId)} → {getAccountNumber(tx.targetAccountId)}
                            </span>
                        </p>
                    </button>
                ))}
            </div>
            </section>
        </div>
    </OverviewCard>
    );
}
