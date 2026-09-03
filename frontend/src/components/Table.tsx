type TableProps = {
    tableType: "transaction" | "account" | "planned";
    children: React.ReactNode;
    handleClick: () => void;
}

/**
 * Renders titled table section, variant chosen by `tableType`. Header + action
 * button above `children` (row list).
 *
 * Variants:
 * - "account": "Mina Konton" / "ändra"
 * - "planned": "Planerade överföringar" / "hantera"
 * - "transaction": "Senaste transaktioner" / "visa alla"
 */

export default function Table({tableType, children, handleClick}: TableProps) {
    switch (tableType) {
        case "account": 
            return (
                <div className="font-montserrat flex flex-col gap-8 min-w-200">
                    <div className="border-b-nordiska-orange border-b-4 flex justify-between items-end">
                        <h1 className="font-semibold text-4xl">Mina Konton</h1>
                        <button className="uppercase text-primary-blue hover:text-nordiska-blue" onClick={() => handleClick()}>ändra</button>
                    </div>
                    {children}
                </div>
            )
        case "planned":
            return (
                <div className="font-montserrat flex flex-col gap-8">
                    <div className="border-b-nordiska-orange border-b-3 flex justify-between">
                        <h1 className="font-semibold text-4xl">Planerade överföringar</h1>
                        <button className="uppercase text-primary-blue hover:text-nordiska-blue" onClick={() => handleClick()}>hantera</button>
                    </div>
                    {children}
                </div>
            )
        case "transaction":
            return (
                <div className="font-montserrat flex flex-col gap-8">
                    <div className="border-b-nordiska-orange border-b-3 flex justify-between">
                        <h1 className="font-semibold text-4xl">Senaste transaktioner</h1>
                        <button className="uppercase text-primary-blue hover:text-nordiska-blue" onClick={() => handleClick()}>visa alla</button>
                    </div>
                    {children}
                </div>
            )
        default:
            return null
    }
}
