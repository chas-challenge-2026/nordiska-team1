import { useTranslation } from "react-i18next";
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
        <div className="p-3 sm:p-4">
            <div className="max-h-[420px] overflow-y-auto pr-1">
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
            <p className="mt-3 ml-1 text-xs text-secondary sm:text-sm">
                {t("page-transfer.pending.footnote")}
            </p>
        </div>
    );
}
