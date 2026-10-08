import { useState } from "react";
import { useTranslation } from "react-i18next";
import Table from "../Table";
import TableRow from "../TableRow";
import PlannedTransferActionsModal from "./PlannedTransferActionsModal";
import type { EditPlannedError } from "./PlannedTransferActionsModal";
import type { PlannedTransfer } from "../../constants/transferAccounts";
import type { PlannedTransferChanges } from "../../hooks/usePlannedTransfers";

type PlannedTransfersPanelProps = {
    upcomingTransfers: PlannedTransfer[];
    onEditTransfer: (
        transfer: PlannedTransfer,
        changes: PlannedTransferChanges,
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
    const [displayedTransfer, setDisplayedTransfer] = useState<PlannedTransfer | null>(null);

    // Mutationerna lever i TransferPage, så deras status nollställs vid öppna/stäng
    // för att ett gammalt fel inte ska synas på nästa överföring.
    const openActions = (transfer: PlannedTransfer) => {
        onResetStatus();
        setActiveTransfer(transfer);
        setDisplayedTransfer(transfer);
    };

    const closeActions = () => {
        onResetStatus();
        setActiveTransfer(null);
    };

    return (
        <div >
            <Table tableType="planned" handleClick={() => { }}>
                <div className="max-h-[420px] overflow-y-auto">
                    {upcomingTransfers.map((planned) => (
                        <TableRow
                            key={planned.localId}
                            rowType="planned"
                            plannedDate={planned.date}
                            plannedName={planned.name}
                            plannedAccounts={
                                planned.fromName && planned.toName
                                    ? `${planned.fromName} → ${planned.toName}`
                                    : undefined
                            }
                            plannedNote={planned.note}
                            plannedSum={planned.sum}
                            plannedActionsLabel={t("page-transfer.planned.actions-label")}
                            onOpenActions={() => openActions(planned)}
                        />
                    ))}
                </div>
            </Table>
            <p className="mt-3 max-w-[42ch] text-sm text-secondary">
                {t("page-transfer.planned.footnote")}
            </p>

            {displayedTransfer && (
                <PlannedTransferActionsModal
                    // Ny instans per överföring så att formuläret börjar från rätt värden.
                    key={displayedTransfer.localId}
                    isOpen={activeTransfer !== null}
                    transfer={displayedTransfer}
                    onClose={closeActions}
                    onSave={(changes) =>
                        onEditTransfer(displayedTransfer, changes, closeActions)
                    }
                    isSaving={isSaving}
                    editError={editError}
                    onConfirmDelete={() =>
                        onDeleteTransfer(displayedTransfer, closeActions)
                    }
                    isDeleting={isDeleting}
                    deleteError={deleteError}
                />
            )}
        </div>
    );
}
