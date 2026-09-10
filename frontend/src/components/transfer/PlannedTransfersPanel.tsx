import { useTranslation } from "react-i18next";
import Table from "../Table";
import TableRow from "../TableRow";
import type { PlannedTransfer } from "../../constants/transferAccounts";

type PlannedTransfersPanelProps = {
    upcomingTransfers: PlannedTransfer[];
};

export default function PlannedTransfersPanel({
    upcomingTransfers,
}: PlannedTransfersPanelProps) {
    const { t } = useTranslation();

    return (
        <div className="border-l border-[#E5EAF0] px-10 pt-8 pb-10">
            <Table tableType="planned" handleClick={() => {}}>
                <div className="max-h-[420px] overflow-y-auto">
                    {upcomingTransfers.map((planned, index) => (
                        <TableRow
                            key={`${planned.date}-${index}`}
                            id={`${planned.date}-${index}`}
                            rowType="planned"
                            plannedDate={planned.date}
                            plannedName={planned.name}
                            plannedNote={planned.note}
                            plannedSum={planned.sum}
                        />
                    ))}
                </div>
            </Table>
            <p className="mt-5.5 max-w-[42ch] text-xs text-secondary">
                {t("page-transfer.planned.footnote")}
            </p>
        </div>
    );
}
