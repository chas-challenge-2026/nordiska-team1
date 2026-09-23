import { useState } from "react";
import type { SubmitEvent } from "react";
import { useTranslation } from "react-i18next";
import Modal from "../modals/Modal";
import InputField from "../forms/InputField";
import { CollapsibleFormBtns } from "../forms/CollapsibleFormButtons";
import type { PlannedTransfer } from "../../constants/transferAccounts";

type View = "menu" | "edit" | "confirm-delete";

type PlannedTransferActionsModalProps = {
    transfer: PlannedTransfer;
    onClose: () => void;
    onSaveDate: (newDate: string) => void;
    onConfirmDelete: () => Promise<void>;
};

/**
 * Meny (Redigera/Ta bort) för en rad i "Planerade överföringar", öppnad via
 * "..."-ikonen. Samma Modal-skal som resten av appen, med tre vyer inuti.
 */
export default function PlannedTransferActionsModal({
    transfer,
    onClose,
    onSaveDate,
    onConfirmDelete,
}: PlannedTransferActionsModalProps) {
    const { t } = useTranslation();
    const [view, setView] = useState<View>("menu");
    const [date, setDate] = useState(transfer.date);
    const [isDeleting, setIsDeleting] = useState(false);
    const [deleteError, setDeleteError] = useState(false);

    const handleSaveDate = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();
        onSaveDate(date);
    };

    const handleConfirmDelete = async () => {
        setIsDeleting(true);
        setDeleteError(false);
        try {
            await onConfirmDelete();
        } catch {
            setDeleteError(true);
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <Modal
            onClose={onClose}
            title={
                view === "edit"
                    ? t("page-transfer.planned.edit-heading")
                    : view === "confirm-delete"
                      ? t("page-transfer.planned.delete-heading")
                      : t("page-transfer.planned.actions-heading")
            }
            widthClassName="w-full max-w-[420px]"
        >
            <div className="border-b border-[#E5EAF0] px-7 py-6 pb-[18px]">
                <h3 className="m-0 text-xl font-semibold text-dark-navy">
                    {view === "edit"
                        ? t("page-transfer.planned.edit-heading")
                        : view === "confirm-delete"
                          ? t("page-transfer.planned.delete-heading")
                          : transfer.name}
                </h3>
            </div>

            {view === "menu" && (
                <div className="flex flex-col gap-1 px-3 py-3">
                    <button
                        type="button"
                        onClick={() => setView("edit")}
                        className="cursor-pointer rounded-md px-4 py-3 text-left text-[15px] font-medium text-dark-navy hover:bg-gray-100"
                    >
                        {t("page-transfer.planned.action-edit")}
                    </button>
                    <button
                        type="button"
                        onClick={() => setView("confirm-delete")}
                        className="cursor-pointer rounded-md px-4 py-3 text-left text-[15px] font-medium text-red-600 hover:bg-gray-100"
                    >
                        {t("page-transfer.planned.action-delete")}
                    </button>
                </div>
            )}

            {view === "edit" && (
                <form
                    onSubmit={handleSaveDate}
                    className="flex flex-1 flex-col gap-5 overflow-y-auto px-7 py-6"
                >
                    <InputField
                        name="plannedTransferDate"
                        type="date"
                        label={t("page-transfer.planned.edit-date-label")}
                        placeholder=""
                        value={date}
                        onChange={setDate}
                    />
                    <CollapsibleFormBtns onClose={onClose} />
                </form>
            )}

            {view === "confirm-delete" && (
                <div className="flex flex-col gap-5 px-7 py-6">
                    <p className="m-0 text-sm text-secondary">
                        {t("page-transfer.planned.delete-confirm-text", {
                            name: transfer.name,
                        })}
                    </p>
                    {deleteError && (
                        <p className="m-0 text-sm text-red-600">
                            {t("page-transfer.planned.delete-error")}
                        </p>
                    )}
                    <div className="flex justify-end gap-6 pt-2">
                        <button
                            type="button"
                            onClick={onClose}
                            disabled={isDeleting}
                            className="cursor-pointer font-bold uppercase text-secondary disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {t("generic.cancel")}
                        </button>
                        <button
                            type="button"
                            onClick={handleConfirmDelete}
                            disabled={isDeleting}
                            className="cursor-pointer font-bold uppercase text-red-600 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {isDeleting
                                ? t("page-transfer.planned.delete-confirm-loading")
                                : t("page-transfer.planned.delete-confirm")}
                        </button>
                    </div>
                </div>
            )}
        </Modal>
    );
}
