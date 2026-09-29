import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import OverviewCard from "./OverviewCard";
import Table from "../Table";
import TableRow from "../TableRow";
import StatusText from "./StatusText";
import { formatDate, formatTime, toTimestamp } from "../../utils/date";
import { useTransactions } from "../../hooks/useTransactions";

const MAX_ROWS = 4;

export default function RecentTransactionsCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: transactions, isPending, isError } = useTransactions();

    const recent = (transactions ?? [])
        .filter((tx) => !tx.isPlanned)
        .sort((a, b) => toTimestamp(b.createdAt) - toTimestamp(a.createdAt))
        .slice(0, MAX_ROWS);

    return (
        <OverviewCard>
            <Table tableType="transaction" handleClick={() => navigate("/transactions")}>
                {isPending ? (
                    <StatusText text={t("transactions-route.loading-transactions")} />
                ) : isError ? (
                    <StatusText text={t("transactions-route.transactions-error")} isError />
                ) : recent.length === 0 ? (
                    <StatusText text={t("overview-route.no-transactions")} />
                ) : (
                    recent.map((tx) => (
                        <TableRow
                            key={tx.id}
                            rowType="transaction"
                            transactionDate={formatDate(tx.createdAt)}
                            transactionTime={formatTime(tx.createdAt)}
                            transactionRecipient={
                                tx.label ||
                                t(tx.type === "Deposit" ? "transactions-route.type-deposit" : "transactions-route.type-withdraw")
                            }
                            transactionAmount={tx.amount}
                        />
                    ))
                )}
            </Table>
        </OverviewCard>
    );
}
