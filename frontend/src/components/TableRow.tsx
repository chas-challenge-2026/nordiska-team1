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
                <div className="border-b border-primary-blue font-montserrat">
                    <p className="flex uppercase text-xs pb-4">{props.plannedDate}</p>
                    <p className="flex justify-between text-xl font-semibold pb-4"><span>{props.plannedName}</span><span>{props.plannedSum.toLocaleString()} sek</span></p>
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
