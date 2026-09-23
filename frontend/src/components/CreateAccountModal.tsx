import { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import Modal from "./modals/Modal";
import InputField from "./forms/InputField";
import { CollapsibleFormBtns } from "./forms/CollapsibleFormButtons";
import { useCreateAccount, useAccountTypes } from "../hooks/useAccounts";
import type { AccountTypes } from "../services/accountsService";
import { useUserStore } from "../store/userStore";

type CreateAccountModalProps = {
    onClose: () => void;
    isModalOpen: boolean;
};

interface AccountTypeOption {
    value: AccountTypes,
    label: string,
    rate: string,
}

interface AccountTypeResponse {
    accountType: AccountTypes;
    interestRate: number;
    description: string;
}

export default function CreateAccountModal({ onClose, isModalOpen = false }: CreateAccountModalProps) {
    const { t } = useTranslation();
    const createAccount = useCreateAccount();
    const { user } = useUserStore();
    const { data: accountTypes, isLoading, error } = useAccountTypes();

    const accountTypeOptions = useMemo<AccountTypeOption[]>(() => {
        if (!accountTypes) return [];
        return accountTypes.map((item: AccountTypeResponse) => ({
            value: item.accountType as AccountTypes,
            label: t(`accounts-route.account-type-${item.accountType}`),
            rate: `${(item.interestRate * 100).toFixed(1)}%`,
        }));
    }, [accountTypes, t]);

    const [accountName, setAccountName] = useState("");
    const [accountType, setAccountType] = useState<AccountTypes | "">("");
    const [initialDeposit, setInitialDeposit] = useState("");
    const [accountTypeError, setAccountTypeError] = useState<string>();
    const [depositError, setDepositError] = useState<string>();

    function handleSubmit(e: React.SubmitEvent) {
        e.preventDefault();

        let hasError = false;

        if (!accountType) {
            setAccountTypeError(t("accounts-route.account-type-required"));
            hasError = true;
        } else {
            setAccountTypeError(undefined);
        }

        const parsedDeposit = initialDeposit.trim() === "" ? undefined : Number(initialDeposit);
        if (parsedDeposit !== undefined && Number.isNaN(parsedDeposit)) {
            setDepositError(t("accounts-route.invalid-amount"));
            hasError = true;
        } else {
            setDepositError(undefined);
        }


        if (hasError || !accountType) return;

        createAccount.mutate(
            {
                customerId: user!.id,
                accountName: accountName.trim() === "" ? null : accountName,
                accountType,
                initialDeposit: parsedDeposit,
            },
            { onSuccess: onClose }
        );
    }

    return (
        <Modal isOpen={isModalOpen} onClose={onClose} title={t("accounts-route.new-account")}>
            <form onSubmit={handleSubmit} className="flex flex-col gap-4 p-6">
                <h2 className="text-lg font-semibold text-dark-navy">{t("accounts-route.new-account")}</h2>

                <InputField
                    name="accountName"
                    type="text"
                    label={t("accounts-route.account-name")}
                    placeholder={t("accounts-route.account-name-placeholder")}
                    value={accountName}
                    onChange={setAccountName}
                />

                <div>
                    <div className="flex items-center justify-between">
                        <label htmlFor="accountType" className="text-sm font-bold text-dark-navy">
                            {t("accounts-route.account-type")} <span className="font-light text-red-600"> *</span>
                        </label>
                        {accountTypeError && (
                            <span id="accountType-error" className="text-sm text-red-700">
                                {accountTypeError}
                            </span>
                        )}
                    </div>
                    <select
                        id="accountType"
                        name="accountType"
                        value={accountType}
                        required
                        disabled={isLoading || !!error}
                        onChange={(e) => setAccountType(e.target.value as AccountTypes)}
                        aria-invalid={!!accountTypeError}
                        aria-describedby={accountTypeError ? "accountType-error" : undefined}
                        className={`mt-1 w-full rounded-md border bg-white px-3 py-2 ${accountTypeError ? "border-red-700" : "border-nordiska-blue"
                            }`}
                    >
                        <option value="" disabled>
                            {isLoading
                                ? t("accounts-route.loading-account-types")
                                : error
                                    ? t("accounts-route.error-account-types")
                                    : t("accounts-route.select-account-type")}
                        </option>
                        {accountTypeOptions.map((opt) => (
                            <option key={opt.value} value={opt.value}>
                                {opt.label} - {t("generic.interest")}: {opt.rate}
                            </option>
                        ))}
                    </select>
                </div>

                <InputField
                    name="initialDeposit"
                    type="number"
                    label={t("accounts-route.initial-deposit")}
                    placeholder="0"
                    value={initialDeposit}
                    onChange={setInitialDeposit}
                    error={depositError}
                    suffix="sek"
                />
                {createAccount.isError && (
                    <p className="text-sm text-red-700">{t("accounts-route.create-account-error")}</p>
                )}

                <CollapsibleFormBtns onClose={onClose} />
            </form>
        </Modal>
    );
}
