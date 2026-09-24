import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatCurrency } from "../utils/currency";

type TransactionRow = {
    rowType: "transaction";
    transactionDate: string;
    transactionTime: string;
    transactionRecipient: string;
    transactionAmount: number;
};

type AccountRow = {
    rowType: "account";
    accountType: string;
    accountNumber: string;
    accountInterest: number;
    accountName: string;
    accountBalance: number;
};

type PlannedRow = {
    rowType: "planned";
    plannedDate: string;
    plannedName: string;
    plannedSum: number;
    plannedNote?: string;
    plannedActionsLabel?: string;
    onOpenActions?: () => void;
};

type RowProps = TransactionRow | AccountRow | PlannedRow;

const ROW_CLASS = "min-w-0 border-b border-primary-blue font-montserrat";

type TwoLineRowProps = {
    detail: ReactNode;
    detailRight: ReactNode;
    name: string;
    amount: string;
    amountClassName?: string;
};

function TwoLineRow({ detail, detailRight, name, amount, amountClassName = "" }: TwoLineRowProps) {
    return (
        <div className={ROW_CLASS}>
            <p className="flex flex-wrap justify-between gap-x-2 gap-y-1 pb-2 text-[10px] uppercase sm:pb-4 sm:text-xs">
                <span className="min-w-0 wrap-break-word">{detail}</span>
                <span className="shrink-0 whitespace-nowrap">{detailRight}</span>
            </p>
            <p className="flex items-baseline justify-between gap-2 pb-3 text-base font-semibold sm:pb-4 sm:text-xl">
                <span className="min-w-0 wrap-break-word">{name}</span>
                <span className={`shrink-0 whitespace-nowrap ${amountClassName}`}>{amount}</span>
            </p>
        </div>
    );
}

export default function TableRow(props: RowProps) {
    const { t } = useTranslation();

    switch (props.rowType) {
        case "transaction": {
            const amount = props.transactionAmount;
            return (
                <TwoLineRow
                    detail={props.transactionDate}
                    detailRight={props.transactionTime}
                    name={props.transactionRecipient}
                    amount={formatCurrency(amount, { signed: true })}
                    amountClassName={amount < 0 ? "text-red-700" : "text-green-700"}
                />
            );
        }
        case "account": {
            const interest = (props.accountInterest).toLocaleString(undefined, {
                style: "percent",
                maximumFractionDigits: 2,
            });
            return (
                <TwoLineRow
                    detail={<>{props.accountType} <span className="font-semibold">{props.accountNumber}</span></>}
                    detailRight={<>{t("generic.interest")} <strong>{interest}</strong></>}
                    name={props.accountName}
                    amount={formatCurrency(props.accountBalance)}
                />
            );
        }
        case "planned":
            return (
                <div className={`${ROW_CLASS} pb-4`}>
                    <div className="flex items-center justify-between gap-2">
                        <div className="flex min-w-0 flex-1 justify-between gap-2 text-sm font-bold sm:text-[15px]">
                            <span className="min-w-0 truncate">{props.plannedName}</span>
                            <span className="shrink-0 whitespace-nowrap">{formatCurrency(props.plannedSum)}</span>
                        </div>
                        {props.onOpenActions && (
                            <button
                                type="button"
                                onClick={props.onOpenActions}
                                aria-label={props.plannedActionsLabel ?? t("page-transfer.planned.actions-label")}
                                className="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full text-secondary hover:bg-gray-100"
                            >
                                <svg width="18" height="18" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                                    <circle cx="10" cy="4" r="1.6" />
                                    <circle cx="10" cy="10" r="1.6" />
                                    <circle cx="10" cy="16" r="1.6" />
                                </svg>
                            </button>
                        )}
                    </div>
                    <p className="mt-1 text-[10px] uppercase tracking-[0.08em] text-secondary sm:text-xs">{props.plannedDate}</p>
                    {props.plannedNote && <p className="wrap-break-word text-xs text-secondary">{props.plannedNote}</p>}
                </div>
            );
    }
}
