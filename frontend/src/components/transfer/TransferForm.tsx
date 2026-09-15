import { useTranslation } from "react-i18next";
import InputField from "../InputField";
import AccountTriggerButton from "./AccountTriggerButton";
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
    name,
    onNameChange,
    isExternal,
    canSubmit,
    ctaHint,
    onSubmit,
}: TransferFormProps) {
    const { t } = useTranslation();

    return (
        <div>
            <div className="flex items-end justify-between border-b-[3px] border-nordiska-orange pb-2.5">
                <h2 className="m-0 text-[26px] font-semibold text-dark-navy">
                    {t("page-transfer.heading")}
                </h2>
            </div>
            <p className="mt-3.5 mb-6.5 max-w-[44ch] text-sm text-secondary">
                {t("page-transfer.help-text")}
            </p>

            <div className="flex flex-col gap-5.5">
                <div className="grid grid-cols-2 gap-5">
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
                        onClick={onOpenToModal}
                    />
                </div>

                <div>
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
                </div>

                <div className="grid grid-cols-2 items-end gap-5">
                    <InputField
                        name="transferDate"
                        type="date"
                        label={t("page-transfer.date-label")}
                        placeholder=""
                        value={date}
                        onChange={onDateChange}
                    />
                    <label className="flex cursor-pointer items-center gap-2.5 pb-2.5 text-sm text-dark-navy">
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

                <div className="flex flex-col items-end gap-2.5 pt-1.5">
                    {ctaHint && (
                        <span className="text-sm text-secondary">
                            {ctaHint}
                        </span>
                    )}
                    <button
                        type="button"
                        onClick={onSubmit}
                        disabled={!canSubmit}
                        className={`cursor-pointer rounded-md border-0 bg-nordiska-blue px-7.5 py-3.5 text-[15px] font-bold text-white hover:bg-login-bg disabled:cursor-not-allowed ${
                            canSubmit ? "opacity-100" : "opacity-[0.45]"
                        }`}
                    >
                        {isExternal
                            ? t("page-transfer.cta-submit-external")
                            : t("page-transfer.cta-submit-internal")}
                    </button>
                </div>
            </div>
        </div>
    );
}
