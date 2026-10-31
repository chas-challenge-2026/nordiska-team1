import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { LoadingState, ErrorState } from "../StatusMessage";
import { formatDate, formatTime, toTimestamp } from "../../utils/date";
import { useTransactions } from "../../hooks/useTransactions";
import { formatCurrencySmallSek } from "../../utils/currency";
import { useGetAccounts } from "../../hooks/useAccounts";
import { useNavigate } from "react-router";
import { type Transaction } from "../../services/transactionsService";
import OverviewCardHeader from "./OverviewCardHeader";
import { OverviewCardRow } from "./OverviewCardRow";
import type { OverviewCardProps } from "../../pages/OverviewPage";

function NoTransactions() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-light-blue-accent mask-[url('/icons/transactions.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold mt-1 text-dark-navy/80">{t("overview-route.transactions-card.no-transactions")}</p>
            <p className="px-4 text-center text-[0.8em] mt-1 text-dark-navy/80">{t("overview-route.transactions-card.no-transactions-description")}</p>
            <p className="mt-4 text-center text-sm font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/transfer")} className="text-primary-blue cursor-pointer hover:text-primary-hover">{t("page-navigation.transfers")}</span></p>
        </div>
    );
}

const MAX_ROWS = 10;

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

export default function RecentTransactionsCard({ showOnlyFavs }: OverviewCardProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: transactions, isPending, isError } = useTransactions({ Page: 1, PageSize: 20, AccountIds: [] });
    const { data: accounts } = useGetAccounts();

    const result: RecentTransaction[] = [];
    const processedTransfers = new Set<number>();

    const recent = (transactions?.items ?? [])
        .filter((tx) => !tx.isPlanned)
        .filter((tx) => {
            if (!showOnlyFavs) return true;
            return accounts?.some( (account) =>  account.id === tx.accountId &&  account.isFavorite);
        })
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
            <OverviewCardHeader title={t("overview-route.transactions-card.title")} description={t("overview-route.transactions-card.description")} />

            {/* USER FEEDBACK */}
            {result.length === 0 && showOnlyFavs && (<NoTransactions />)}
            {isPending && ( <LoadingState title={t("overview-route.transactions-card.pending")} />)}
            {isError && ( <ErrorState title={t("overview-route.transactions-card.error")} />)}

            {/* CONTENT ROWS */}
            {!isPending && !isError && (
                <section className="flex min-h-0 flex-1 flex-col ">
                    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
                        <div className="min-h-0 flex-1 overflow-y-auto">

                            {result.slice(0, MAX_ROWS).map((tx) => {

                                const displayName = tx.label ? tx.label : t(`overview-route.transactions-card.transfer-type-${tx.type}`);
                                const colorAmount = tx.type === "transfer-group" ? "neutral" : tx.amount < 0 ? "minus" : "plus";
                                const displayDate = <>{formatTime(tx.createdAt)} <span className="ml-1">{formatDate(tx.createdAt)}</span></>;
                                const displayType = tx.type === "transfer-group" ? `${getAccountNumber(tx.from)} → ${getAccountNumber(tx.to)}` : getAccountNumber(tx.accountId);
                                            
                                return (
                                    <OverviewCardRow 
                                        key={tx.id}
                                        btnHoverText={`${t("overview-route.transactions-card.hover-title")} ${formatDate(tx.createdAt)}`}
                                        onClick={() => handleTransactionClick(tx)}
                                        cols={"2"}
                                        titleHoverText={displayName}
                                        titleRow={[displayName, formatCurrencySmallSek(tx.amount)]}
                                        colorAmount={colorAmount}
                                        descriptionRow={[displayDate, displayType]}
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