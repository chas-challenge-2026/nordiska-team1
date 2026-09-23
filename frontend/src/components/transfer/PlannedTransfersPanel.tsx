import { useState } from "react";
import { useTranslation } from "react-i18next";
import Table from "../Table";
import TableRow from "../TableRow";
import PlannedTransferActionsModal from "./PlannedTransferActionsModal";
import type { PlannedTransfer } from "../../constants/transferAccounts";

type PlannedTransfersPanelProps = {
    upcomingTransfers: PlannedTransfer[];
    onEditTransfer: (transfer: PlannedTransfer, newDate: string) => void;
    onDeleteTransfer: (transfer: PlannedTransfer) => void;
};

export default function PlannedTransfersPanel({
    upcomingTransfers,
    onEditTransfer,
    onDeleteTransfer,
}: PlannedTransfersPanelProps) {
    const { t } = useTranslation();
    const [activeTransfer, setActiveTransfer] = useState<PlannedTransfer | null>(null);

    return (
        <div className="border-t border-[#E5EAF0] px-4 pt-6 pb-6 sm:px-6 sm:pt-8 sm:pb-8 lg:border-t-0 lg:border-l lg:px-10 lg:pt-8 lg:pb-10">
            <Table tableType="planned" handleClick={() => {}}>
                <div className="max-h-[420px] overflow-y-auto">
                    {upcomingTransfers.map((planned) => (
                        <TableRow
                            key={planned.localId}
                            id={planned.localId}
                            rowType="planned"
                            plannedDate={planned.date}
                            plannedName={planned.name}
                            plannedNote={planned.note}
                            plannedSum={planned.sum}
                            plannedActionsLabel={t("page-transfer.planned.actions-label")}
                            onOpenActions={() => setActiveTransfer(planned)}
                        />
                    ))}
                </div>
            </Table>
            <p className="mt-5.5 max-w-[42ch] text-xs text-secondary">
                {t("page-transfer.planned.footnote")}
            </p>

            {activeTransfer && (
                <PlannedTransferActionsModal
                    transfer={activeTransfer}
                    onClose={() => setActiveTransfer(null)}
                    onSaveDate={(newDate) => {
                        onEditTransfer(activeTransfer, newDate);
                        setActiveTransfer(null);
                    }}
                    onConfirmDelete={() => {
                        onDeleteTransfer(activeTransfer);
                        setActiveTransfer(null);
                    }}
                />
            )}
        </div>
    );
}
