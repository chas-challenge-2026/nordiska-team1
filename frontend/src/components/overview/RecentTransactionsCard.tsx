import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { LoadingState, ErrorState } from "../StatusMessage";
import { formatDate, formatTime, toTimestamp } from "../../utils/date";
import { useTransactions } from "../../hooks/useTransactions";
import { formatCurrency } from "../../utils/currency";
import { useGetAccounts } from "../../hooks/useAccounts";
import { useNavigate } from "react-router";
import { type Transaction } from "../../services/transactionsService";

const MAX_ROWS = 5;

type TransferGroup = {
    id: string;
    type: "transfer-group";
    createdAt: string;
    label?: string;
    from: number;
    to: number;
    amount: number;
    transactions: Transaction[];
};

type RecentTransaction = Transaction | TransferGroup;

export default function RecentTransactionsCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: transactions, isPending, isError } = useTransactions({ Page: 1, PageSize: 14, AccountIds: [] });
    const { data: accounts } = useGetAccounts();

    const result: RecentTransaction[] = [];
    const processedTransfers = new Set<number>();

    const recent = (transactions?.items ?? [])
        .filter((tx) => !tx.isPlanned)
        .sort((a, b) => toTimestamp(b.createdAt) - toTimestamp(a.createdAt))

    // PARA IHOP TRANSAKTIONER OCH SKAPA NY LISTA
    for (const transaction of recent) {
        // sortera bort transfer
        if (transaction.type !== "transfer") { result.push(transaction); continue; }
        // den här transfern har redan grupperats?
        if (processedTransfers.has(transaction.id)) { continue; }
        // hitta en matchande transfer  - matchar överföring på: type, omatchande id, date, label och summa
        const matchingTransfer = recent.find(other =>
            other.type === "transfer" &&
            other.id !== transaction.id &&
            other.createdAt === transaction.createdAt &&
            other.label === transaction.label &&
            Math.abs(other.amount) === Math.abs(transaction.amount)
        );
        // om det finns matchande
        if (matchingTransfer) {
            result.push({
                id: `transfer-${transaction.id}-${matchingTransfer.id}`,
                type: "transfer-group",
                createdAt: transaction.createdAt,
                label: transaction.label,
                from: transaction.amount < 0 ? transaction.accountId : matchingTransfer.accountId,
                to: transaction.amount > 0 ? transaction.accountId : matchingTransfer.accountId,
                amount: Math.abs(transaction.amount),
                transactions: [ transaction, matchingTransfer ]
            });
            processedTransfers.add(transaction.id);
            processedTransfers.add(matchingTransfer.id);
        } else {
            // om ingen match hittades push som den är
            result.push(transaction);
        }
    }

    const getAccountNumber = (accountId: number | string) => {
        const account = accounts?.find((account) => account.id === accountId);
        if (account?.status === "closed"){ return t("overview-route.transactions-card.closed-account")}
        return account?.accountNumber ?? "";
    };

    const handleTransactionClick = (tx: RecentTransaction) => {
        const params = new URLSearchParams();
        const date = new Date(tx.createdAt).toLocaleDateString("sv-SE");
        params.set("dateFrom", date);
        params.set("dateTo", date);

        if (tx.label) {params.set("search", tx.label);}
        const accountIds = tx.type === "transfer-group" ? tx.transactions.map(transaction => transaction.accountId) : [tx.accountId];
        params.set("accountIds", [...new Set(accountIds)].join(","));
        navigate(`/transactions?${params.toString()}`);
    };

    return (
    <OverviewCard>
        <div className="flex h-full flex-col">

            {/* CARD HEADER */}
            <div className="h-22 bg-dark-navy pl-2 pt-1.5 text-white sm:pl-3">
                <div className="flex flex-col items-start p-3 sm:p-4">
                    <h2 className="text-xl font-semibold tracking-wide sm:text-2xl"> {t("overview-route.transactions-card.title")} </h2>
                    <p className="text-[0.65em] uppercase text-white/80 sm:text-[0.70em]"> {t("overview-route.transactions-card.description")} </p>
                </div>
            </div>

            {/* USER FEEDBACK */}
            {isPending && ( <LoadingState title={t("overview-route.transactions-card.pending")} />)}
            {isError && ( <ErrorState title={t("overview-route.transactions-card.error")} />)}

            {/* CONTENT */}
            {!isPending && !isError && (
                <section className="flex flex-1 flex-col p-2 sm:p-3">
                <div className="flex flex-1 flex-col overflow-hidden">

                    {/* TRANSFER ROWS */}
                    {result.slice(0, MAX_ROWS).map((tx, index) => (
                        <button 
                            key={tx.id}
                            type="button"
                            onClick={() => handleTransactionClick(tx)}
                            className={`flex basis-1/5 shrink-0 flex-col justify-center px-3 py-2  border-nordiska-orange odd:bg-light-gray/60 even:bg-white sm:px-5 cursor-pointer hover:bg-primary-blue/10 ${index === 3 ? "last:border-0" : "border-b-2"}`}
                            >
        
                            {/* NAMN & SUMMA */}
                            <p className="flex items-baseline justify-between gap-2 text-sm sm:text-base">
                                <span className="min-w-0 wrap-break-word font-semibold"> {tx.label ? tx.label : t(`overview-route.transactions-card.transfer-type-${tx.type}`)} </span>
                                <span className={`shrink-0 whitespace-nowrap font-medium ${tx.amount < 0 ? "text-red-700" : tx.type === "transfer-group" ? "" : "text-green-700"}`}>
                                    {tx.type === "transfer-group" ? "" : tx.amount < 0 ? "" : "+"}
                                    {formatCurrency(tx.amount)}
                                </span>
                            </p>

                            {/* TID & TRANSAKTIONSTYP / FRÅN TILL */}
                            <p className="flex flex-wrap justify-between gap-x-2 gap-y-1 text-[9px] uppercase text-dark-navy/60 sm:text-xs">
                                <span className="min-w-0 wrap-break-word"> {formatTime(tx.createdAt)} <span className="ml-1">{formatDate(tx.createdAt)}</span> </span> 
                                <span className="shrink-0 whitespace-nowrap">
                                    {tx.type === "transfer-group"
                                        ? `${getAccountNumber(tx.from)} → ${getAccountNumber(tx.to)}`
                                        : t(`overview-route.transactions-card.transfer-type-${tx.type}`)
                                    }
                                </span>
                            </p>
                        </button>
                    
                    ))}
                </div>
                </section>
            )}
        </div>
    </OverviewCard>
    );
}