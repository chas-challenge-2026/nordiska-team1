import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

type TableType = "account" | "planned" | "transaction" | "savings";

type TableProps = {
    tableType: TableType;
    children: ReactNode;
    handleClick?: () => void;
};

const HEADERS: Record<TableType, { titleKey: string; actionKey?: string }> = {
    account: { titleKey: "table.my-accounts", actionKey: "generic.edit" },
    planned: { titleKey: "table.planned-transactions" },
    transaction: { titleKey: "table.latest-transactions", actionKey: "generic.show-all" },
    savings: { titleKey: "overview-route.savings-title" },
};

export default function Table({ tableType, children, handleClick }: TableProps) {
    const { t } = useTranslation();
    const { titleKey, actionKey } = HEADERS[tableType];

    return (
        <div className="flex w-full min-w-0 flex-col gap-4 font-montserrat sm:gap-8">
            <div className="flex items-end justify-between gap-4 border-b-3 border-b-nordiska-orange pb-2.5">
                <h2 className="min-w-0 wrap-break-word text-xl font-semibold sm:text-[26px]">{t(titleKey)}</h2>
                {actionKey && handleClick && (
                    <button
                        type="button"
                        onClick={handleClick}
                        className="shrink-0 cursor-pointer whitespace-nowrap text-sm uppercase text-primary-blue hover:text-nordiska-blue sm:text-base"
                    >
                        {t(actionKey)}
                    </button>
                )}
            </div>
            {children}
        </div>
    );
}
