import { useState } from "react";
import { useTranslation } from "react-i18next";
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
        <div className="flex min-h-0 flex-1 flex-col p-3 sm:p-4">
            <p className="mt-3 ml-1 shrink-0 text-xs text-secondary sm:text-sm">
                {t("page-transfer.planned.footnote")}
            </p>
            {upcomingTransfers.length === 0 ? (
                <div className="border border-nordiska-blue/50 bg-light-gray/30 p-4 text-center">
                    <span aria-hidden="true" className="mx-auto block h-10 w-10 bg-nordiska-orange mask-[url('/icons/transfers.svg')] mask-contain mask-center mask-no-repeat" />
                    <p className="mt-2 text-sm font-semibold text-dark-navy/80">{t("no-planned-state.title")}</p>
                    <p className="mt-1 text-xs text-dark-navy/70">{t("no-planned-state.description")}</p>
                </div>
            ) : (
                <div className="min-h-0 flex-1 overflow-y-auto pr-1">
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
            )}

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
