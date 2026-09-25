import { useState } from "react";
import { useTranslation } from "react-i18next";
import Table from "../Table";
import TableRow from "../TableRow";
import PlannedTransferActionsModal from "./PlannedTransferActionsModal";
import type { EditPlannedError } from "./PlannedTransferActionsModal";
import type { PlannedTransfer } from "../../constants/transferAccounts";

type PlannedTransfersPanelProps = {
    upcomingTransfers: PlannedTransfer[];
    onEditTransfer: (
        transfer: PlannedTransfer,
        newDate: string,
        onSaved: () => void,
    ) => void;
    onDeleteTransfer: (transfer: PlannedTransfer, onDeleted: () => void) => void;
    isSaving: boolean;
    editError: EditPlannedError;
    isDeleting: boolean;
    deleteError: boolean;
    onResetStatus: () => void;
};

export default function PlannedTransfersPanel({
    upcomingTransfers,
    onEditTransfer,
    onDeleteTransfer,
    isSaving,
    editError,
    isDeleting,
    deleteError,
    onResetStatus,
}: PlannedTransfersPanelProps) {
    const { t } = useTranslation();
    const [activeTransfer, setActiveTransfer] = useState<PlannedTransfer | null>(null);

    // Mutationerna lever i TransferPage, så deras status nollställs vid öppna/stäng
    // för att ett gammalt fel inte ska synas på nästa överföring.
    const openActions = (transfer: PlannedTransfer) => {
        onResetStatus();
        setActiveTransfer(transfer);
    };

    const closeActions = () => {
        onResetStatus();
        setActiveTransfer(null);
    };

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
                            onOpenActions={() => openActions(planned)}
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
                    onClose={closeActions}
                    onSaveDate={(newDate) =>
                        onEditTransfer(activeTransfer, newDate, closeActions)
                    }
                    isSaving={isSaving}
                    editError={editError}
                    onConfirmDelete={() =>
                        onDeleteTransfer(activeTransfer, closeActions)
                    }
                    isDeleting={isDeleting}
                    deleteError={deleteError}
                />
            )}
        </div>
    );
}
