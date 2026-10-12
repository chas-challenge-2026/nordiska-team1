import { useTranslation } from "react-i18next";
import Modal from "../modals/Modal";

type CloseAccountModalProps = {
    isConfirmOpen: boolean;
    expandedAccount?: {
        accountType: string;
        accountName: string | null;
        accountNumber: string;
    };
    closeAccountError: Error | null;
    isPending: boolean;
    onClose: () => void;
    onConfirm: () => void;
};

export default function CloseAccountModal({
    isConfirmOpen,
    expandedAccount,
    closeAccountError,
    isPending,
    onClose,
    onConfirm,
}: CloseAccountModalProps) {
    const { t } = useTranslation();

    return (
        <Modal
            isOpen={isConfirmOpen}
            onClose={onClose}
            title={t("accounts-route.close-confirmation")}
        >
            <div className="flex flex-col gap-4 p-5 sm:p-6">
                <p className="font-semibold text-lg text-red-700"> {t("accounts-route.close-account-info")}</p>
                <p className="text-sm text-secondary/70 -mb-3 font-semibold uppercase">
                {t(`accounts-route.account-type-${expandedAccount?.accountType}`)}
                </p>

                <div className="flex gap-5">
                    <p className="mt-1 text-lg font-semibold text-dark-navy"> {expandedAccount?.accountName} </p>
                    <p className="mt-1 text-lg text-dark-navy"> {expandedAccount?.accountNumber} </p>
                </div>

                {closeAccountError && (
                    <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm font-medium text-red-700">
                        {closeAccountError instanceof Error ? closeAccountError.message : t("accounts-route.close-error")}
                    </p>
                )}

                <p className="mt-1 text-sm text-right text-red-700"> {t("accounts-route.close-confirmation")} </p>

                <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
                    <button
                        type="button"
                        onClick={onClose}
                        disabled={isPending}
                        className="min-h-11 rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-semibold text-dark-navy transition cursor-pointer hover:bg-gray-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        {t("generic.cancel")}
                    </button>

                    <button
                        type="button"
                        onClick={onConfirm}
                        disabled={isPending}
                        className="min-h-11 rounded-lg bg-red-600 px-4 py-2.5 text-sm font-semibold text-white transition cursor-pointer hover:bg-red-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-600 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        {isPending ? t("accounts-route.closing") : t("accounts-route.close-account")}
                    </button>
                </div>
            </div>
        </Modal>
    );
}