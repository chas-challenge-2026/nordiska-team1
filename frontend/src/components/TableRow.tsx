type BaseRowProps = {
    id: string;
    onClick?: () => void;
    className?: string;
};

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

type RowProps = BaseRowProps & (TransactionRow | AccountRow | PlannedRow);

/**
 * Renders single table row, variant chosen by `rowType`.
 *
 * Variants:
 * - "transaction": date/time + recipient/amount
 * - "planned": date + name/sum
 * - "account": type/number/interest + name/balance
 *
 */

export default function TableRow(props: RowProps) {
    switch (props.rowType) {
        case "transaction":
            return (
                <div className="border-b border-primary-blue font-montserrat">
                    <p className="flex justify-between uppercase text-xs pb-4">
                        <span>{props.transactionDate}</span><span>kl {props.transactionTime}</span>
                    </p>
                    <p className="flex justify-between text-xl font-semibold pb-4">
                        <span>{props.transactionRecipient}</span>
                        <span>{props.transactionAmount.toLocaleString()} sek</span>
                    </p>
                </div>
            )
        case "planned":
            return (
                <div className="border-b border-primary-blue font-montserrat pb-4">
                    <div className="flex items-center justify-between gap-2">
                        <div className="flex min-w-0 flex-1 justify-between text-[15px] font-bold">
                            <span className="truncate">{props.plannedName}</span>
                            <span>{props.plannedSum.toLocaleString()} sek</span>
                        </div>
                        {props.onOpenActions && (
                            <button
                                type="button"
                                onClick={props.onOpenActions}
                                aria-label={props.plannedActionsLabel ?? "Actions"}
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
                    <p className="mt-1 flex uppercase text-xs tracking-[0.08em] text-secondary">{props.plannedDate}</p>
                    {props.plannedNote && (
                        <p className="text-xs text-secondary">{props.plannedNote}</p>
                    )}
                </div>
            )
        case "account":
            return (
                <div className="border-b border-primary-blue font-montserrat">
                    <p className="flex justify-between uppercase text-xs pb-4">
                        <span>{props.accountType} <span className="font-semibold">{props.accountNumber}</span>
                        </span><span>Ränta <strong>{props.accountInterest}%</strong></span>
                    </p>
                    <p className="flex justify-between text-xl font-semibold pb-4">
                        <span>{props.accountName}</span>
                        <span>{props.accountBalance.toLocaleString()} sek</span>
                    </p>
                </div>
            )
        default:
            return (
                <div className="border-b border-primary-blue font-montserrat">
                    <p className="font-bold text-2xl">
                        Någonting gick fel vid hämtning av datan.
                    </p>
                </div>
            )
    }
}
