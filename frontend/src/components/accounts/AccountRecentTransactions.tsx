import { useTranslation } from "react-i18next";
import { useTransactions } from "../../hooks/useTransactions";
import { formatCurrencySmallSek } from "../../utils/currency";
import { formatDate, formatTime, toTimestamp } from "../../utils/date";
import { LoadingState, ErrorState } from "../StatusMessage";

type AccountRecentTransactionsProps = {
    accountId: number;
};

export default function AccountRecentTransactions({accountId}: AccountRecentTransactionsProps) {
    const { t } = useTranslation();
    const { data: transactions, isPending, isError,} = useTransactions({Page: 1, PageSize: 5, AccountIds: [accountId],});

    const recentTransactions = (transactions?.items ?? [])
        .filter((tx) => !tx.isPlanned)
        .sort((a, b) => toTimestamp(b.createdAt) - toTimestamp(a.createdAt),)

    return (
        <section aria-labelledby="account-recent-transactions-heading" className="bg-white -mt-1">
            <h2 id="savings-goal-heading" className="bg-dark-navy text-base font-medium text-white sm:text-lg pt-1 pl-2">{t("accounts-route.transactions-section.title")}</h2>
            
            <div className="border border-dark-navy/50 bg-light-gray/30 sm:p-2">
                {/* LOADING & ERROR */}
                {isPending && ( <LoadingState title={t("overview-route.transactions-card.pending")}/>)}
                {isError && (<ErrorState title={t("overview-route.transactions-card.error")}/>)}

                {/* TRANSACTIONS */}
                {!isPending && !isError && (
                    <>
                        {recentTransactions.length === 0 
                        ? ( <p className="text-sm text-dark-navy">{t("accounts-route.transactions-section.no-transactions")}</p>) 
                        : (
                            recentTransactions.map((tx) => (
                                <div key={tx.id} className="w-full flex items-center justify-between border-b border-primary-hover/50 py-1.5 last:border-0 odd:bg-white">
                                    <div className="flex flex-1 min-w-0 items-center gap-2 mx-1">
                                        {/* DATE, TID */}
                                        <div className="grid shrink-0 grid-cols-[2fr_1fr] text-xs uppercase text-dark-navy/60 sm:w-32 sm:text-sm">
                                            <span>{formatDate(tx.createdAt)}</span>
                                            <span>{formatTime(tx.createdAt)}</span>
                                        </div>
                                        {/* TITEL */}
                                        <span className="truncate min-w-0 flex-1 wrap-break-word text-xs font-semibold sm:text-sm">
                                            {tx.label || t(`overview-route.transactions-card.transfer-type-${tx.type}`)}
                                        </span>
                                        {/* SUMMA */}
                                        <span className={`ml-2 shrink-0 whitespace-nowrap text-right text-xs font-medium sm:text-sm ${tx.amount < 0 ? "text-red-700" : "text-green-700"}`}>
                                            {tx.amount > 0 ? "+" : ""}{formatCurrencySmallSek(tx.amount)}
                                        </span>
                                    </div> 
                                </div>
                            ))
                        )}
                    </>
                )}
            </div>
        </section>
    );
}
