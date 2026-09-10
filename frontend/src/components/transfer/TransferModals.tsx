import { useTranslation } from "react-i18next";
import Modal from "../modals/Modal";
import AccountPickerModal from "../modals/AccountPickerModal";
import type { AccountPickerGroup } from "../modals/AccountPickerModal";
import AddAccountForm from "../modals/AddAccountForm";
import type { NewAccountValues } from "../modals/AddAccountForm";
import BankIdConfirm from "../modals/BankIdConfirm";
import { formatSek } from "./transferHelpers";
import type { TransferAccount } from "../../constants/transferAccounts";

export type ModalKind = "from" | "to" | null;

type TransferModalsProps = {
    modal: ModalKind;
    addAccountOpen: boolean;
    search: string;
    onSearchChange: (value: string) => void;
    groups: AccountPickerGroup[];
    isEmpty: boolean;
    onSelectAccount: (id: string) => void;
    onCloseModal: () => void;
    onOpenAdd: () => void;
    onCancelAdd: () => void;
    onSaveAdd: (values: NewAccountValues) => void;

    showBankId: boolean;
    fromAccount: TransferAccount | null;
    toAccount: TransferAccount | null;
    amountValue: number;
    date: string;
    onApprove: () => void;
    onCancelBankId: () => void;
};

export default function TransferModals({
    modal,
    addAccountOpen,
    search,
    onSearchChange,
    groups,
    isEmpty,
    onSelectAccount,
    onCloseModal,
    onOpenAdd,
    onCancelAdd,
    onSaveAdd,
    showBankId,
    fromAccount,
    toAccount,
    amountValue,
    date,
    onApprove,
    onCancelBankId,
}: TransferModalsProps) {
    const { t } = useTranslation();

    return (
        <>
            {modal !== null && (
                <Modal
                    onClose={onCloseModal}
                    title={
                        addAccountOpen
                            ? t("page-transfer.add-account.heading")
                            : modal === "from"
                              ? t("page-transfer.modal.from-title")
                              : t("page-transfer.modal.to-title")
                    }
                    widthClassName="w-[560px]"
                    maxHeightClassName="max-h-[620px]"
                >
                    {addAccountOpen ? (
                        <AddAccountForm
                            onCancel={onCancelAdd}
                            onSave={onSaveAdd}
                        />
                    ) : (
                        <AccountPickerModal
                            title={
                                modal === "from"
                                    ? t("page-transfer.modal.from-title")
                                    : t("page-transfer.modal.to-title")
                            }
                            search={search}
                            onSearchChange={onSearchChange}
                            groups={groups}
                            isEmpty={isEmpty}
                            onSelect={onSelectAccount}
                            onClose={onCloseModal}
                            onAddNew={modal === "to" ? onOpenAdd : undefined}
                        />
                    )}
                </Modal>
            )}

            {showBankId && toAccount && (
                <Modal
                    onClose={onCancelBankId}
                    title={t("page-transfer.bankid.heading")}
                    widthClassName="w-[480px]"
                >
                    <BankIdConfirm
                        amountFormatted={formatSek(amountValue)}
                        toName={toAccount.name}
                        toMeta={toAccount.meta}
                        fromName={fromAccount ? fromAccount.name : ""}
                        date={date}
                        onApprove={onApprove}
                        onCancel={onCancelBankId}
                    />
                </Modal>
            )}
        </>
    );
}
