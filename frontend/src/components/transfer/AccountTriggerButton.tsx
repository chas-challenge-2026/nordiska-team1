import { useTranslation } from "react-i18next";

type AccountTriggerButtonProps = {
    label: string;
    name: string;
    meta: string;
    balance?: string;
    onClick: () => void;
};

export default function AccountTriggerButton({
    label,
    name,
    meta,
    balance,
    onClick,
}: AccountTriggerButtonProps) {
    const { t } = useTranslation();

    return (
        <div>
            <label className="mb-1.5 block text-[13px] font-bold text-dark-navy">
                {label}
            </label>
            <button
                type="button"
                onClick={onClick}
                className="flex min-h-11 w-full cursor-pointer flex-col justify-center gap-0.5 rounded-md border border-nordiska-blue bg-white px-3.5 py-2.5 text-left"
            >
                <span className="flex items-baseline justify-between gap-3">
                    <span className="truncate text-[15px] font-semibold text-dark-navy">
                        {name}
                    </span>
                    {balance && (
                        <span className="flex-none text-[15px] text-dark-navy">
                            {balance}
                        </span>
                    )}
                </span>
                <span className="flex items-center justify-between gap-3">
                    <span className="truncate text-xs text-secondary">
                        {meta}
                    </span>
                    <span className="flex-none text-xs font-bold whitespace-nowrap text-primary-blue uppercase tracking-[0.08em]">
                        {t("page-transfer.select")}
                    </span>
                </span>
            </button>
        </div>
    );
}
