import { useTranslation } from "react-i18next";

type BankIdConfirmProps = {
    amountFormatted: string;
    toName: string;
    toMeta: string;
    fromName: string;
    date: string;
    onApprove: () => void;
    onCancel: () => void;
};

/**
 * BankID-bekräftelse, endast för externa mottagare. Interna överföringar
 * hoppar över detta steg.
 */
export default function BankIdConfirm({
    amountFormatted,
    toName,
    toMeta,
    fromName,
    date,
    onApprove,
    onCancel,
}: BankIdConfirmProps) {
    const { t } = useTranslation();

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-overlay p-10">
            <div className="animate-rise w-[480px] rounded-xl bg-white p-8 shadow-modal">
                <h3 className="m-0 text-[22px] font-semibold text-dark-navy">
                    {t("transfer.bankid.heading")}
                </h3>
                <p className="mt-2 mb-5.5 text-sm text-secondary">
                    {t("transfer.bankid.help-text")}
                </p>

                <div className="flex flex-col gap-3 rounded-lg border border-border-light px-5 py-4.5">
                    <div className="flex justify-between gap-4">
                        <span className="text-sm text-meta">{t("transfer.bankid.amount")}</span>
                        <span className="text-xl font-bold text-dark-navy">{amountFormatted} sek</span>
                    </div>
                    <div className="h-px bg-hairline" />
                    <div className="flex justify-between gap-4">
                        <span className="text-sm text-meta">{t("transfer.bankid.to")}</span>
                        <span className="text-right">
                            <span className="block text-[15px] font-bold text-dark-navy">{toName}</span>
                            <span className="block text-sm text-meta">{toMeta}</span>
                        </span>
                    </div>
                    <div className="flex justify-between gap-4">
                        <span className="text-sm text-meta">{t("transfer.bankid.from")}</span>
                        <span className="text-[15px] font-bold text-dark-navy">{fromName}</span>
                    </div>
                    <div className="flex justify-between gap-4">
                        <span className="text-sm text-meta">{t("transfer.bankid.date")}</span>
                        <span className="text-[15px] font-bold text-dark-navy">{date}</span>
                    </div>
                </div>

                <button
                    type="button"
                    onClick={onApprove}
                    className="mt-6 w-full cursor-pointer rounded-md border-0 bg-nordiska-blue px-0 py-3.5 text-base font-bold text-white hover:bg-login-bg"
                >
                    {t("transfer.bankid.approve")}
                </button>
                <button
                    type="button"
                    onClick={onCancel}
                    className="mt-3 w-full cursor-pointer border-0 bg-none py-3 text-sm font-bold text-primary-blue"
                >
                    {t("generic.cancel")}
                </button>
            </div>
        </div>
    );
}
