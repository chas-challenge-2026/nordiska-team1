import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { toTimestamp } from "../../utils/date";
import { useTransactions } from "../../hooks/useTransactions";
import { LoadingState, ErrorState } from "../StatusMessage";
import { useNavigate } from "react-router";

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
    // Stopgap: backend cannot filter isPlanned yet, planned transfers beyond first page are missed
    const { data: transactions, isPending, isError } = useTransactions({ Page: 1, PageSize: 100, AccountIds: [] });

    const startOfToday = new Date().setHours(0, 0, 0, 0);

    const upcoming = (transactions?.items ?? [])
        .filter((tx) => tx.isPlanned && toTimestamp(tx.plannedDate) >= startOfToday)
        .sort((a, b) => toTimestamp(a.plannedDate) - toTimestamp(b.plannedDate))
        .slice(0, MAX_ROWS);

    return (
    <OverviewCard>
        <div className="flex h-full flex-col">
            
            {/* CARD HEADER */}
            <div className="h-22 rounded-t-3xl bg-dark-navy pl-2 pt-1.5 text-white sm:pl-3">
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
            <div className="flex flex-1 flex-col rounded-b-xl overflow-hidden">

            </div>
            </section>
        
        </div>
    </OverviewCard>
    );
}

{/* 
<Table tableType="planned">
    {isPending ? (
        <StatusText text={t("overview-route.loading-planned")} />
    ) : isError ? (
        <StatusText text={t("overview-route.planned-error")} isError />
    ) : upcoming.length === 0 ? (
        <StatusText text={t("overview-route.no-planned")} />
    ) : (
        upcoming.map((tx) => (
            <TableRow
                key={tx.id}
                rowType="planned"
                plannedDate={formatDate(tx.plannedDate)}
                plannedName={tx.label || t("overview-route.planned-unnamed")}
                plannedSum={Math.abs(tx.amount)}
            />
        ))
    )}
</Table> */}