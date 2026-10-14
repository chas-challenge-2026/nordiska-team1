import { useTranslation } from "react-i18next";
import InputField from "../forms/InputField";
import AccountTriggerButton from "./AccountTriggerButton";
import RecurrenceFields from "./RecurrenceFields";
import type { RecurrenceFieldsValue } from "./RecurrenceFields";
import { formatSek } from "./transferHelpers";
import type { OwnAccount, TransferAccount } from "../../constants/transferAccounts";

type TransferFormProps = {
    fromAccount: OwnAccount | null;
    toAccount: TransferAccount | null;
    onOpenFromModal: () => void;
    onOpenToModal: () => void;
    amount: string;
    onAmountChange: (value: string) => void;
    over: boolean;
    date: string;
    onDateChange: (value: string) => void;
    recurring: boolean;
    onRecurringChange: (value: boolean) => void;
    recurrence: RecurrenceFieldsValue;
    name: string;
    onNameChange: (value: string) => void;
    isExternal: boolean;
    canSubmit: boolean;
    ctaHint: string;
    onSubmit: () => void;
};

export default function TransferForm({
    fromAccount,
    toAccount,
    onOpenFromModal,
    onOpenToModal,
    amount,
    onAmountChange,
    over,
    date,
    onDateChange,
    recurring,
    onRecurringChange,
    recurrence,
    name,
    onNameChange,
    isExternal,
    canSubmit,
    ctaHint,
    onSubmit,
}: TransferFormProps) {
    const { t } = useTranslation();

    return (
        <div className="p-3 sm:p-4">
            {/* ACCOUNTS */}
            <section aria-labelledby="transfer-accounts-heading">
                <h3 id="transfer-accounts-heading" className="mb-2 ml-1 text-sm font-semibold text-dark-navy sm:text-base">
                    {t("page-transfer.section-accounts")}
                </h3>
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 sm:gap-4">
                    <AccountTriggerButton
                        label={t("page-transfer.from-label")}
                        name={
                            fromAccount
                                ? fromAccount.name
                                : t("page-transfer.select-account-placeholder")
                        }
                        meta={
                            fromAccount
                                ? fromAccount.meta
                                : t("page-transfer.from-meta-placeholder")
                        }
                        balance={
                            fromAccount
                                ? `${formatSek(fromAccount.balance)} sek`
                                : undefined
                        }
                        onClick={onOpenFromModal}
                    />
                    <AccountTriggerButton
                        label={t("page-transfer.to-label")}
                        name={
                            toAccount
                                ? toAccount.name
                                : t(
                                      "page-transfer.select-recipient-placeholder",
                                  )
                        }
                        meta={
                            toAccount
                                ? toAccount.meta
                                : t("page-transfer.to-meta-placeholder")
                        }
                        balance={
                            toAccount?.own
                                ? `${formatSek(toAccount.balance)} sek`
                                : undefined
                        }
                        onClick={onOpenToModal}
                    />
                </div>
            </section>

            {/* DIVIDER */}
            <div aria-hidden="true" className="mx-auto mt-4 w-[99%] border-b-2 border-nordiska-orange sm:mt-6" />

            {/* DETAILS */}
            <section aria-labelledby="transfer-details-heading" className="mt-4 sm:mt-6">
                <h3 id="transfer-details-heading" className="mb-2 ml-1 text-sm font-semibold text-dark-navy sm:text-base">
                    {t("page-transfer.section-details")}
                </h3>
                <div className="flex flex-col gap-4 border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:gap-5 sm:p-4">
                    <InputField
                        name="transferAmount"
                        type="text"
                        label={t("page-transfer.amount-label")}
                        placeholder={t("page-transfer.amount-placeholder")}
                        value={amount}
                        onChange={(value) =>
                            onAmountChange(value.replace(/[^\d ,]/g, ""))
                        }
                        suffix={t("page-transfer.amount-suffix")}
                        error={
                            over
                                ? t("page-transfer.amount-error", {
                                      account: fromAccount?.name ?? "",
                                  })
                                : undefined
                        }
                    />

                    <div className="grid grid-cols-1 items-end gap-4 sm:grid-cols-2 sm:gap-5">
                        <InputField
                            name="transferDate"
                            type="date"
                            label={t("page-transfer.date-label")}
                            placeholder=""
                            value={date}
                            onChange={onDateChange}
                        />
                        <label className="flex cursor-pointer items-center gap-2.5 pb-2.5 text-sm font-medium text-dark-navy">
                            <input
                                type="checkbox"
                                checked={recurring}
                                onChange={(e) =>
                                    onRecurringChange(e.target.checked)
                                }
                                className="h-4.5 w-4.5 cursor-pointer accent-[var(--color-nordiska-blue)]"
                            />
                            {t("page-transfer.recurring-label")}
                        </label>
                    </div>

                    {recurring && (
                        <RecurrenceFields name="transfer" {...recurrence} />
                    )}

                    <InputField
                        name="transferName"
                        type="text"
                        label={t(
                            recurring
                                ? "page-transfer.name-label-recurring"
                                : "page-transfer.name-label",
                        )}
                        placeholder={t("page-transfer.name-placeholder")}
                        value={name}
                        onChange={onNameChange}
                    />
                </div>
            </section>

            {/* DIVIDER */}
            <div aria-hidden="true" className="mx-auto mt-4 w-[99%] border-b-2 border-nordiska-orange sm:mt-6" />

            {/* SUBMIT */}
            <div className="mt-4 mb-2 flex flex-col items-stretch gap-2 sm:mt-6 sm:flex-row sm:items-center sm:justify-end sm:gap-4">
                {ctaHint && (
                    <span className="text-center text-sm text-secondary sm:text-right">
                        {ctaHint}
                    </span>
                )}
                <button
                    type="button"
                    onClick={onSubmit}
                    disabled={!canSubmit}
                    className="inline-flex cursor-pointer items-center justify-center rounded-lg bg-primary-blue px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-nordiska-blue disabled:cursor-not-allowed disabled:opacity-45 disabled:hover:bg-primary-blue"
                >
                    {isExternal
                        ? t("page-transfer.cta-submit-external")
                        : t("page-transfer.cta-submit-internal")}
                </button>
            </div>
        </div>
    );
}
