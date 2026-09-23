import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import Table from "../Table";
import TableRow from "../TableRow";
import StatusText from "./StatusText";
import { formatDate, toTimestamp } from "../../utils/date";
import { useTransactions } from "../../hooks/useTransactions";

const MAX_ROWS = 4;

export default function PlannedTransfersCard() {
    const { t } = useTranslation();
    const { data: transactions, isPending, isError } = useTransactions();

    // setHours returns the new timestamp: local midnight today.
    const startOfToday = new Date().setHours(0, 0, 0, 0);

    const upcoming = (transactions ?? [])
        .filter((tx) => tx.isPlanned && toTimestamp(tx.plannedDate) >= startOfToday)
        .sort((a, b) => toTimestamp(a.plannedDate) - toTimestamp(b.plannedDate))
        .slice(0, MAX_ROWS);

    return (
        <OverviewCard>
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
            </Table>
        </OverviewCard>
    );
}
