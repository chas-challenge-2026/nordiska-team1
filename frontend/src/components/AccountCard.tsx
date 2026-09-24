import { useState } from "react";
import { useCloseAccount } from "../hooks/useAccounts";
import type { Account } from "../services/accountsService";
import { formatCurrency } from "../utils/currency";
import { useTranslation } from "react-i18next";
import Modal from "./modals/Modal";

interface AccountCardProps {
    account: Account;
}

export default function AccountCard({ account }: AccountCardProps) {
    const estimatedAnnualInterest = account.balance * ((account.interestRate * 100) / 100);
    const { mutate: closeAccount, isError, error, isPending, reset } = useCloseAccount();
    const { t } = useTranslation();
    const [isConfirmOpen, setIsConfirmOpen] = useState(false);

    const canClose = account.balance === 0;

    function handleConfirmClose() {
        closeAccount(account.id, {
            onSuccess: () => setIsConfirmOpen(false),
        });
    }

    function handleOpenConfirm() {
        reset();
        setIsConfirmOpen(true);
    }

    return (
        <article className="flex flex-col gap-4 rounded-lg border border-secondary/15 bg-white p-4 shadow-card sm:p-5">
            <div>
                <p className="font-semibold text-dark-navy">{account.accountName}</p>
                <p className="flex flex-wrap items-center gap-2 text-sm text-secondary">
                    <span>{t(`accounts-route.account-type-${account.accountType}`)} {account.accountNumber}</span>
                    |
                    <span>{t("generic.interest")} {(account.interestRate * 100).toLocaleString("sv-SE")}%</span>
                    {canClose && (
                        <button
                            onClick={handleOpenConfirm}
                            className="ml-auto border border-transparent text-red-700 cursor-pointer text-xs hover:border-secondary p-1 rounded-lg hover:text-red-500"
                        >
                            {t("accounts-route.close-account")}
                        </button>
                    )}
                </p>
            </div>

            <div>
                <p className="text-sm text-secondary">{t("generic.balance")}</p>
                <p className="font-montserrat-alternates text-2xl font-semibold tracking-tight text-dark-navy sm:text-3xl">
                    {formatCurrency(account.balance)}
                </p>
            </div>

            <div className="flex flex-wrap items-end justify-between gap-3 border-t border-secondary/15 pt-4">
                <div className="min-w-0">
                    <p className="text-sm text-secondary">{t("accounts-route.yearly-interest")}</p>
                    <p className="text-sm font-semibold text-dark-navy">
                        {formatCurrency(estimatedAnnualInterest)}
                    </p>
                    <p className="text-xs leading-snug text-secondary">
                        {t("accounts-route.yearly-interest-info")}
                    </p>
                </div>
                <button
                    type="button"
                    onClick={() => { }}
                    className="whitespace-nowrap rounded-md bg-primary-blue px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-nordiska-blue focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-light-blue-accent"
                >
                    {t("generic.tax-report")}
                </button>
            </div>

            <Modal isOpen={isConfirmOpen} onClose={() => setIsConfirmOpen(false)} title={t("accounts-route.close-confirmation")}>
                <div className="flex flex-col gap-4 p-5">
                    <p className="text-dark-navy">
                        {t("accounts-route.close-account-warning", { accountNumber: account.accountNumber })}
                    </p>

                    {isError && (
                        <p role="alert" className="text-sm text-red-600">
                            {error instanceof Error ? error.message : t("accounts-route.close-error")}
                        </p>
                    )}

                    <div className="flex justify-end gap-3">
                        <button
                            type="button"
                            onClick={() => setIsConfirmOpen(false)}
                            disabled={isPending}
                            className="rounded-md px-3 py-2 text-sm font-semibold text-dark-navy hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {t("generic.cancel")}
                        </button>
                        <button
                            type="button"
                            onClick={handleConfirmClose}
                            disabled={isPending}
                            className="rounded-md bg-red-700 px-3 py-2 text-sm font-semibold text-white hover:bg-red-600 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {isPending ? t("accounts-route.closing") : t("accounts-route.close-account")}
                        </button>
                    </div>
                </div>
            </Modal>
        </article>
    );
}
