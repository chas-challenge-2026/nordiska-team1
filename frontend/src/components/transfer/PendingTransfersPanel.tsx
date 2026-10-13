import { useTranslation } from "react-i18next";
import Table from "../Table";
import TableRow from "../TableRow";
import type { PendingTransfer } from "../../hooks/usePendingTransfers";

type PendingTransfersPanelProps = {
    pendingTransfers: PendingTransfer[];
};

/** Externa betalningar som skickats men inte är framme än. Går inte att ändra. */
export default function PendingTransfersPanel({
    pendingTransfers,
}: PendingTransfersPanelProps) {
    const { t } = useTranslation();

    return (
        <div>
            <Table tableType="pending">
                <div className="max-h-[420px] overflow-y-auto">
                    {pendingTransfers.map((pending) => (
                        <TableRow
                            key={pending.id}
                            rowType="planned"
                            plannedDate={t("page-transfer.pending.arrives", {
                                date: pending.arrivesAt,
                            })}
                            plannedName={pending.name}
                            plannedAccounts={`${pending.fromName} → ${pending.toName}`}
                            plannedSum={pending.sum}
                        />
                    ))}
                </div>
            </Table>
            <p className="mt-3 max-w-[42ch] text-sm text-secondary">
                {t("page-transfer.pending.footnote")}
            </p>
        </div>
    );
}
